#!/bin/sh
# Wire-format tests (needs mono). Run from the zombie-pile folder:  sh Tools/tests/run.sh
set -e
mcs -out:/tmp/netcodec_test.exe Assets/Scripts/Net/NetCodec.cs Tools/tests/NetCodecTest.cs
mono /tmp/netcodec_test.exe
(cd server && npm test)
