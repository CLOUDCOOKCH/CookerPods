// SPDX-License-Identifier: MIT
// Original CookerPods implementation using the documented Microsoft Bluetooth
// profile-driver interface. See README.md for API provenance and validation limits.
#include <ntddk.h>
#include <wdf.h>
#include <initguid.h>
#include <bthdef.h>
#include <bthguid.h>
#include <bthioctl.h>
#include <bthddi.h>
#include "ControlContract.h"

#define CP_POOL_TAG 'pkoC'

typedef struct _CP_DEVICE {
    WDFDEVICE Device;
    WDFIOTARGET Target;
    WDFWAITLOCK Gate;
    WDFWORKITEM DisconnectWork;
    BTH_PROFILE_DRIVER_INTERFACE Profile;
    WDFREQUEST CloseRequest;
    struct _BRB_L2CA_CLOSE_CHANNEL* CloseBrb;
    BTH_ADDR Address;
    L2CAP_CHANNEL_HANDLE Channel;
    volatile LONG RemoteDisconnected;
    BOOLEAN ProfileValid;
    BOOLEAN Powered;
} CP_DEVICE;
WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(CP_DEVICE, CpContext)

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD CpDeviceAdd;
EVT_WDF_OBJECT_CONTEXT_CLEANUP CpCleanup;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL CpIoctl;
EVT_WDF_FILE_CLEANUP CpFileCleanup;
EVT_WDF_DEVICE_D0_ENTRY CpD0Entry;
EVT_WDF_DEVICE_D0_EXIT CpD0Exit;
EVT_WDF_WORKITEM CpDisconnectWorker;

// Gate serializes all passive-level channel work. No client-supplied address,
// PSM, kernel pointer, IOCTL forwarding or arbitrary packet writing is exposed.
static NTSTATUS CpSubmit(CP_DEVICE* ctx, PBRB brb, ULONG bytes, ULONG timeoutMs)
{
    WDF_MEMORY_DESCRIPTOR argument;
    WDF_REQUEST_SEND_OPTIONS options;
    NTSTATUS status;
    WDF_MEMORY_DESCRIPTOR_INIT_BUFFER(&argument, brb, bytes);
    WDF_REQUEST_SEND_OPTIONS_INIT(&options, WDF_REQUEST_SEND_OPTION_TIMEOUT);
    WDF_REQUEST_SEND_OPTIONS_SET_TIMEOUT(&options, WDF_REL_TIMEOUT_IN_MS(timeoutMs));
    status = WdfIoTargetSendInternalIoctlOthersSynchronously(ctx->Target, NULL,
        IOCTL_INTERNAL_BTH_SUBMIT_BRB, &argument, NULL, NULL, &options, NULL);
    // STATUS_TIMEOUT is informational in NTSTATUS; it must not become success.
    return status == STATUS_TIMEOUT ? STATUS_IO_TIMEOUT : status;
}

static NTSTATUS CpClose(CP_DEVICE* ctx)
{
    WDF_MEMORY_DESCRIPTOR argument;
    WDF_REQUEST_SEND_OPTIONS options;
    WDF_REQUEST_REUSE_PARAMS reuse;
    NTSTATUS status;
    if (ctx->Channel == NULL || !ctx->ProfileValid) return STATUS_SUCCESS;
    // Close resources are reserved before accepting a connection: teardown must
    // not depend on a new pool allocation while memory is scarce.
    WDF_REQUEST_REUSE_PARAMS_INIT(&reuse, WDF_REQUEST_REUSE_NO_FLAGS, STATUS_SUCCESS);
    status = WdfRequestReuse(ctx->CloseRequest, &reuse);
    if (!NT_SUCCESS(status)) return status;
    ctx->Profile.BthReuseBrb((PBRB)ctx->CloseBrb, BRB_L2CA_CLOSE_CHANNEL);
    ctx->CloseBrb->BtAddress = ctx->Address;
    ctx->CloseBrb->ChannelHandle = ctx->Channel;
    WDF_MEMORY_DESCRIPTOR_INIT_BUFFER(&argument, ctx->CloseBrb, sizeof(*ctx->CloseBrb));
    WDF_REQUEST_SEND_OPTIONS_INIT(&options, WDF_REQUEST_SEND_OPTION_TIMEOUT);
    WDF_REQUEST_SEND_OPTIONS_SET_TIMEOUT(&options, WDF_REL_TIMEOUT_IN_SEC(2));
    status = WdfIoTargetSendInternalIoctlOthersSynchronously(ctx->Target, ctx->CloseRequest,
        IOCTL_INTERNAL_BTH_SUBMIT_BRB, &argument, NULL, NULL, &options, NULL);
    if (status == STATUS_TIMEOUT) status = STATUS_IO_TIMEOUT;
    if (NT_SUCCESS(status) || status == STATUS_DEVICE_NOT_CONNECTED || status == STATUS_DEVICE_REMOVED) {
        ctx->Channel = NULL;
        InterlockedExchange(&ctx->RemoteDisconnected, 0);
        return STATUS_SUCCESS;
    }
    // Retain the failed channel rather than claim it closed/reconnect over it.
    InterlockedExchange(&ctx->RemoteDisconnected, 1);
    return status;
}

// Bluetooth invokes this at up to DISPATCH_LEVEL. Only flag/enqueue here; the
// passive worker acknowledges disconnect outside the Bluetooth callback stack.
static VOID CpIndication(PVOID context, INDICATION_CODE code, PINDICATION_PARAMETERS parameters)
{
    CP_DEVICE* ctx = (CP_DEVICE*)context;
    UNREFERENCED_PARAMETER(parameters);
    if (code == IndicationRemoteDisconnect) {
        InterlockedExchange(&ctx->RemoteDisconnected, 1);
        WdfWorkItemEnqueue(ctx->DisconnectWork);
    }
}

VOID CpDisconnectWorker(WDFWORKITEM work)
{
    CP_DEVICE* ctx = CpContext(WdfWorkItemGetParentObject(work));
    WdfWaitLockAcquire(ctx->Gate, NULL);
    if (InterlockedCompareExchange(&ctx->RemoteDisconnected, 0, 0) != 0) CpClose(ctx);
    WdfWaitLockRelease(ctx->Gate);
}

static NTSTATUS CpConnect(CP_DEVICE* ctx)
{
    BTH_DEVICE_INFO info;
    WDF_MEMORY_DESCRIPTOR output;
    WDF_REQUEST_SEND_OPTIONS options;
    struct _BRB_L2CA_OPEN_CHANNEL* brb;
    NTSTATUS status;
    if (!ctx->Powered) return STATUS_DEVICE_NOT_READY;
    if (ctx->Channel != NULL && !ctx->RemoteDisconnected) return STATUS_SUCCESS;
    status = CpClose(ctx);
    if (!NT_SUCCESS(status)) return status;
    RtlZeroMemory(&info, sizeof(info));
    WDF_MEMORY_DESCRIPTOR_INIT_BUFFER(&output, &info, sizeof(info));
    WDF_REQUEST_SEND_OPTIONS_INIT(&options, WDF_REQUEST_SEND_OPTION_TIMEOUT);
    WDF_REQUEST_SEND_OPTIONS_SET_TIMEOUT(&options, WDF_REL_TIMEOUT_IN_SEC(3));
    // Obtain only the address of the service PDO this driver is bound to.
    status = WdfIoTargetSendInternalIoctlSynchronously(ctx->Target, NULL,
        IOCTL_INTERNAL_BTHENUM_GET_DEVINFO, NULL, &output, &options, NULL);
    if (status == STATUS_TIMEOUT) return STATUS_IO_TIMEOUT;
    if (!NT_SUCCESS(status)) return status;
    if (info.address == 0) return STATUS_DEVICE_NOT_CONNECTED;
    if ((info.flags & BDIF_PAIRED) == 0) return STATUS_ACCESS_DENIED;
    ctx->Address = info.address;
    brb = (struct _BRB_L2CA_OPEN_CHANNEL*)ctx->Profile.BthAllocateBrb(BRB_L2CA_OPEN_CHANNEL, CP_POOL_TAG);
    if (brb == NULL) return STATUS_INSUFFICIENT_RESOURCES;
    brb->BtAddress = ctx->Address;
    brb->Psm = CP_AAP_PSM;
    brb->ChannelFlags = CF_ROLE_EITHER | CF_LINK_ENCRYPTED | CF_LINK_SUPPRESS_PIN;
    brb->ConfigOut.Flags = CFG_MTU;
    brb->ConfigOut.Mtu.Min = L2CAP_MIN_MTU;
    brb->ConfigOut.Mtu.Max = CP_MAX_PACKET;
    brb->ConfigOut.Mtu.Preferred = 1024;
    brb->ConfigIn.Flags = CFG_MTU;
    brb->ConfigIn.Mtu.Min = L2CAP_MIN_MTU;
    brb->ConfigIn.Mtu.Max = CP_MAX_PACKET;
    brb->ConfigIn.Mtu.Preferred = 1024;
    brb->CallbackFlags = CALLBACK_DISCONNECT;
    brb->Callback = CpIndication;
    brb->CallbackContext = ctx;
    brb->ReferenceObject = WdfDeviceWdmGetDeviceObject(ctx->Device);
    brb->IncomingQueueDepth = 10;
    InterlockedExchange(&ctx->RemoteDisconnected, 0);
    status = CpSubmit(ctx, (PBRB)brb, sizeof(*brb), 5000);
    if (NT_SUCCESS(status)) ctx->Channel = brb->ChannelHandle;
    ctx->Profile.BthFreeBrb((PBRB)brb);
    if (ctx->RemoteDisconnected) { CpClose(ctx); return STATUS_DEVICE_NOT_CONNECTED; }
    return status;
}

static BOOLEAN CpAllowedWrite(const UCHAR* bytes, SIZE_T size)
{
    static const UCHAR handshake[] = {0,0,4,0,1,0,2,0,0,0,0,0,0,0,0,0};
    static const UCHAR features[] = {4,0,4,0,0x4d,0,0xff,0,0,0,0,0,0,0};
    static const UCHAR notifications[] = {4,0,4,0,0x0f,0,0xff,0xff,0xfe,0xff};
    if (size == sizeof(handshake) && RtlCompareMemory(bytes, handshake, size) == size) return TRUE;
    if (size == sizeof(features) && RtlCompareMemory(bytes, features, size) == size) return TRUE;
    if (size == sizeof(notifications) && RtlCompareMemory(bytes, notifications, size) == size) return TRUE;
    // Only noise modes and two explicitly bounded boolean settings. No rename,
    // firmware, vendor spoofing, hearing-aid calibration or arbitrary writes.
    return size == 11 && bytes[0] == 4 && bytes[1] == 0 && bytes[2] == 4 && bytes[3] == 0 &&
        bytes[4] == 9 && bytes[5] == 0 && bytes[8] == 0 && bytes[9] == 0 && bytes[10] == 0 &&
        ((bytes[6] == 0x0d && bytes[7] >= 1 && bytes[7] <= 4) ||
         ((bytes[6] == 0x0a || bytes[6] == 0x28) && (bytes[7] == 1 || bytes[7] == 2)));
}

static NTSTATUS CpTransfer(CP_DEVICE* ctx, PVOID buffer, ULONG* length, BOOLEAN receive)
{
    struct _BRB_L2CA_ACL_TRANSFER* brb;
    NTSTATUS status;
    if (!ctx->Powered || ctx->Channel == NULL || ctx->RemoteDisconnected) return STATUS_DEVICE_NOT_CONNECTED;
    if (*length == 0 || *length > CP_MAX_PACKET) return STATUS_INVALID_BUFFER_SIZE;
    brb = (struct _BRB_L2CA_ACL_TRANSFER*)ctx->Profile.BthAllocateBrb(BRB_L2CA_ACL_TRANSFER, CP_POOL_TAG);
    if (brb == NULL) return STATUS_INSUFFICIENT_RESOURCES;
    brb->BtAddress = ctx->Address;
    brb->ChannelHandle = ctx->Channel;
    brb->TransferFlags = receive ? (ACL_TRANSFER_DIRECTION_IN | ACL_SHORT_TRANSFER_OK) : ACL_TRANSFER_DIRECTION_OUT;
    brb->BufferSize = *length;
    brb->Buffer = buffer;
    brb->BufferMDL = NULL;
    status = CpSubmit(ctx, (PBRB)brb, sizeof(*brb), receive ? 1000 : 3000);
    if (NT_SUCCESS(status)) {
        if (brb->BufferSize > *length) status = STATUS_DATA_OVERRUN;
        else *length = brb->BufferSize;
    }
    ctx->Profile.BthFreeBrb((PBRB)brb);
    return status;
}

VOID CpIoctl(WDFQUEUE queue, WDFREQUEST request, SIZE_T outputSize, SIZE_T inputSize, ULONG code)
{
    CP_DEVICE* ctx = CpContext(WdfIoQueueGetDevice(queue));
    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;
    SIZE_T resultSize = 0;
    PVOID buffer = NULL;
    ULONG length;
    WdfWaitLockAcquire(ctx->Gate, NULL);
    if (WdfRequestIsCanceled(request)) status = STATUS_CANCELLED;
    else switch (code) {
    case IOCTL_CP_STATUS:
        if (inputSize != 0 || outputSize < sizeof(CP_STATUS)) { status = STATUS_INVALID_BUFFER_SIZE; break; }
        status = WdfRequestRetrieveOutputBuffer(request, sizeof(CP_STATUS), &buffer, NULL);
        if (NT_SUCCESS(status)) {
            CP_STATUS* value = (CP_STATUS*)buffer;
            RtlZeroMemory(value, sizeof(*value));
            value->AbiVersion = CP_ABI_VERSION;
            value->Connected = ctx->Powered && ctx->Channel != NULL && !ctx->RemoteDisconnected;
            value->MaxPacketBytes = CP_MAX_PACKET;
            resultSize = sizeof(*value);
        }
        break;
    case IOCTL_CP_CONNECT:
    case IOCTL_CP_DISCONNECT:
        if (inputSize != 0 || outputSize != 0) { status = STATUS_INVALID_BUFFER_SIZE; break; }
        if (code == IOCTL_CP_CONNECT) status = CpConnect(ctx);
        else status = CpClose(ctx);
        break;
    case IOCTL_CP_SEND:
        if (inputSize == 0 || inputSize > 32 || outputSize != 0) { status = STATUS_INVALID_BUFFER_SIZE; break; }
        status = WdfRequestRetrieveInputBuffer(request, inputSize, &buffer, NULL);
        if (!NT_SUCCESS(status)) break;
        if (!CpAllowedWrite((const UCHAR*)buffer, inputSize)) { status = STATUS_ACCESS_DENIED; break; }
        length = (ULONG)inputSize;
        status = CpTransfer(ctx, buffer, &length, FALSE);
        if (NT_SUCCESS(status) && length != inputSize) status = STATUS_DEVICE_PROTOCOL_ERROR;
        break;
    case IOCTL_CP_RECEIVE:
        if (inputSize != 0 || outputSize < CP_MAX_PACKET || outputSize > CP_MAX_PACKET) { status = STATUS_INVALID_BUFFER_SIZE; break; }
        status = WdfRequestRetrieveOutputBuffer(request, CP_MAX_PACKET, &buffer, NULL);
        if (!NT_SUCCESS(status)) break;
        RtlZeroMemory(buffer, CP_MAX_PACKET);
        length = CP_MAX_PACKET;
        status = CpTransfer(ctx, buffer, &length, TRUE);
        if (NT_SUCCESS(status)) resultSize = length;
        break;
    default: break;
    }
    if (WdfRequestIsCanceled(request)) { status = STATUS_CANCELLED; resultSize = 0; }
    WdfWaitLockRelease(ctx->Gate);
    WdfRequestCompleteWithInformation(request, status, NT_SUCCESS(status) ? resultSize : 0);
}

VOID CpFileCleanup(WDFFILEOBJECT file)
{
    CP_DEVICE* ctx = CpContext(WdfFileObjectGetDevice(file));
    WdfWaitLockAcquire(ctx->Gate, NULL);
    CpClose(ctx);
    WdfWaitLockRelease(ctx->Gate);
}

NTSTATUS CpD0Entry(WDFDEVICE device, WDF_POWER_DEVICE_STATE previous)
{
    CP_DEVICE* ctx = CpContext(device);
    UNREFERENCED_PARAMETER(previous);
    WdfWaitLockAcquire(ctx->Gate, NULL);
    ctx->Powered = TRUE;
    WdfWaitLockRelease(ctx->Gate);
    return STATUS_SUCCESS;
}

NTSTATUS CpD0Exit(WDFDEVICE device, WDF_POWER_DEVICE_STATE target)
{
    CP_DEVICE* ctx = CpContext(device);
    UNREFERENCED_PARAMETER(target);
    WdfWaitLockAcquire(ctx->Gate, NULL);
    ctx->Powered = FALSE;
    CpClose(ctx);
    WdfWaitLockRelease(ctx->Gate);
    WdfWorkItemFlush(ctx->DisconnectWork);
    return STATUS_SUCCESS;
}

VOID CpCleanup(WDFOBJECT object)
{
    CP_DEVICE* ctx = CpContext(object);
    // D0Exit and file cleanup close channels while the target is operational.
    if (ctx->CloseBrb != NULL && ctx->ProfileValid) ctx->Profile.BthFreeBrb((PBRB)ctx->CloseBrb);
    if (ctx->ProfileValid && ctx->Profile.Interface.InterfaceDereference != NULL) {
        ctx->Profile.Interface.InterfaceDereference(ctx->Profile.Interface.Context);
        ctx->ProfileValid = FALSE;
    }
}

NTSTATUS CpDeviceAdd(WDFDRIVER driver, PWDFDEVICE_INIT init)
{
    WDFDEVICE device;
    WDF_OBJECT_ATTRIBUTES attributes, childAttributes;
    WDF_FILEOBJECT_CONFIG files;
    WDF_PNPPOWER_EVENT_CALLBACKS power;
    WDF_IO_QUEUE_CONFIG queue;
    WDF_WORKITEM_CONFIG work;
    CP_DEVICE* ctx;
    NTSTATUS status;
    UNICODE_STRING security = RTL_CONSTANT_STRING(L"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGW;;;IU)");
    UNREFERENCED_PARAMETER(driver);
    WdfDeviceInitSetExclusive(init, TRUE);
    WdfDeviceInitSetDeviceType(init, FILE_DEVICE_UNKNOWN);
    WdfDeviceInitSetIoType(init, WdfDeviceIoBuffered);
    WdfDeviceInitSetCharacteristics(init, FILE_DEVICE_SECURE_OPEN | FILE_AUTOGENERATED_DEVICE_NAME, TRUE);
    status = WdfDeviceInitAssignSDDLString(init, &security);
    if (!NT_SUCCESS(status)) return status;
    WDF_FILEOBJECT_CONFIG_INIT(&files, WDF_NO_EVENT_CALLBACK, WDF_NO_EVENT_CALLBACK, CpFileCleanup);
    WdfDeviceInitSetFileObjectConfig(init, &files, WDF_NO_OBJECT_ATTRIBUTES);
    WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&power);
    power.EvtDeviceD0Entry = CpD0Entry;
    power.EvtDeviceD0Exit = CpD0Exit;
    WdfDeviceInitSetPnpPowerEventCallbacks(init, &power);
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, CP_DEVICE);
    attributes.ExecutionLevel = WdfExecutionLevelPassive;
    attributes.SynchronizationScope = WdfSynchronizationScopeNone;
    attributes.EvtCleanupCallback = CpCleanup;
    status = WdfDeviceCreate(&init, &attributes, &device);
    if (!NT_SUCCESS(status)) return status;
    ctx = CpContext(device);
    ctx->Device = device;
    ctx->Target = WdfDeviceGetIoTarget(device);
    WDF_OBJECT_ATTRIBUTES_INIT(&childAttributes);
    childAttributes.ParentObject = device;
    status = WdfWaitLockCreate(&childAttributes, &ctx->Gate);
    if (!NT_SUCCESS(status)) return status;
    WDF_WORKITEM_CONFIG_INIT(&work, CpDisconnectWorker);
    work.AutomaticSerialization = FALSE;
    status = WdfWorkItemCreate(&work, &childAttributes, &ctx->DisconnectWork);
    if (!NT_SUCCESS(status)) return status;
    status = WdfFdoQueryForInterface(device, &GUID_BTHDDI_PROFILE_DRIVER_INTERFACE,
        (PINTERFACE)&ctx->Profile, sizeof(ctx->Profile), BTHDDI_PROFILE_DRIVER_INTERFACE_VERSION_FOR_QI, NULL);
    if (!NT_SUCCESS(status)) return status;
    ctx->ProfileValid = TRUE;
    ctx->CloseBrb = (struct _BRB_L2CA_CLOSE_CHANNEL*)ctx->Profile.BthAllocateBrb(BRB_L2CA_CLOSE_CHANNEL, CP_POOL_TAG);
    if (ctx->CloseBrb == NULL) return STATUS_INSUFFICIENT_RESOURCES;
    status = WdfRequestCreate(&childAttributes, ctx->Target, &ctx->CloseRequest);
    if (!NT_SUCCESS(status)) return status;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queue, WdfIoQueueDispatchSequential);
    queue.PowerManaged = WdfFalse;
    queue.EvtIoDeviceControl = CpIoctl;
    status = WdfIoQueueCreate(device, &queue, WDF_NO_OBJECT_ATTRIBUTES, NULL);
    if (!NT_SUCCESS(status)) return status;
    return WdfDeviceCreateDeviceInterface(device, &GUID_DEVINTERFACE_COOKERPODS_CONTROL, NULL);
}

NTSTATUS DriverEntry(PDRIVER_OBJECT object, PUNICODE_STRING registry)
{
    WDF_DRIVER_CONFIG config;
    WDF_DRIVER_CONFIG_INIT(&config, CpDeviceAdd);
    return WdfDriverCreate(object, registry, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
}
