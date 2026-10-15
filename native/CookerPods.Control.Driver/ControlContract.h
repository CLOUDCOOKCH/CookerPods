// SPDX-License-Identifier: MIT
#pragma once

#define CP_ABI_VERSION 1u
#define CP_MAX_PACKET 2048u
#define CP_AAP_PSM 0x1001u
#define CP_IOCTL(n) CTL_CODE(FILE_DEVICE_UNKNOWN, (n), METHOD_BUFFERED, FILE_READ_DATA | FILE_WRITE_DATA)
#define IOCTL_CP_STATUS     CP_IOCTL(0x800)
#define IOCTL_CP_CONNECT    CP_IOCTL(0x801)
#define IOCTL_CP_DISCONNECT CP_IOCTL(0x802)
#define IOCTL_CP_SEND       CP_IOCTL(0x803)
#define IOCTL_CP_RECEIVE    CP_IOCTL(0x804)

// Fixed-width ABI, identical on x64 and ARM64. No pointers or radio identifiers.
typedef struct _CP_STATUS {
    ULONG AbiVersion;
    ULONG Connected;
    ULONG MaxPacketBytes;
    ULONG Reserved;
} CP_STATUS;
C_ASSERT(sizeof(CP_STATUS) == 16);

// {504F4443-4F4F-4B43-8940-55204AA6F610}
DEFINE_GUID(GUID_DEVINTERFACE_COOKERPODS_CONTROL,
    0x504f4443, 0x4f4f, 0x4b43, 0x89, 0x40, 0x55, 0x20, 0x4a, 0xa6, 0xf6, 0x10);
