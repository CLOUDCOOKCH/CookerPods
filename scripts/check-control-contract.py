#!/usr/bin/env python3
"""Check that the native and managed transport use the same fixed ABI."""
from pathlib import Path
import re
root = Path(__file__).resolve().parent.parent
header = (root / 'native/CookerPods.Control.Driver/ControlContract.h').read_text()
transport = (root / 'native/CookerPods.Diagnostic.Windows/DriverAapTransport.cs').read_text()
protocol = (root / 'native/CookerPods.Diagnostic.Core/AapProtocol.cs').read_text()
assert '#define CP_ABI_VERSION 1u' in header
assert 'ReadUInt32LittleEndian(status) != 1' in transport
maximum = int(re.search(r'#define CP_MAX_PACKET (\d+)u', header)[1])
assert maximum == int(re.search(r'MaxPacketBytes = (\d+)', protocol)[1])
for native, managed in [('STATUS', 'Status'), ('CONNECT', 'Connect'), ('SEND', 'Send'), ('RECEIVE', 'Receive')]:
    function = int(re.search(r'#define IOCTL_CP_' + native + r'\s+CP_IOCTL\((0x[0-9a-f]+)\)', header)[1], 16)
    expected = (0x22 << 16) | (3 << 14) | (function << 2)
    actual = int(re.search(r'\b' + managed + r' = (0x[0-9a-f]+)', transport)[1], 16)
    assert actual == expected, native
assert 'C_ASSERT(sizeof(CP_STATUS) == 16)' in header
assert '504f4443-4f4f-4b43-8940-55204aa6f610' in transport
print('Control ABI version, fixed status size, packet bound and IOCTL values agree.')
