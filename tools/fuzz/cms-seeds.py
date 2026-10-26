"""Construct inspection seeds from OpenSSL output without changing any signed attributes.

Called only by external-seeds.sh; these are parser inputs, not qualification evidence.
"""

import pathlib
import sys


def children(value):
    result = []
    pos = 0
    while pos < len(value):
        start = pos
        tag, length = value[pos:pos + 2]
        pos += 2
        if length & 128:
            count = length & 127
            assert count and pos + count <= len(value)
            length = int.from_bytes(value[pos:pos + count], "big")
            pos += count
        end = pos + length
        assert end <= len(value)
        result.append((tag, value[pos:end], value[start:end]))
        pos = end
    return result


def tlv(tag, content):
    size = len(content)
    length = bytes([size]) if size < 128 else size.to_bytes((size.bit_length() + 7) // 8, "big")
    if size >= 128:
        length = bytes([128 | len(length)]) + length
    return bytes([tag]) + length + content


mode, source = sys.argv[1:3]
info = children(children(pathlib.Path(source).read_bytes())[0][1])
signed = children(children(info[1][1])[0][1])
signer = children(children(signed[-1][1])[0][1])
if mode == "signature":
    # The last field of this OpenSSL SignerInfo is the signature OCTET STRING.
    assert signer[-1][0] == 4
    pathlib.Path(sys.argv[3]).write_bytes(signer[-1][1])
elif mode == "attach":
    token = pathlib.Path(sys.argv[3]).read_bytes()
    # id-aa-signatureTimeStampToken, SET OF ContentInfo, [1] IMPLICIT unsigned attributes.
    oid = bytes.fromhex("060b2a864886f70d010910020e")
    attribute = tlv(0x30, oid + tlv(0x31, token))
    signer_info = tlv(0x30, b"".join(field[2] for field in signer) + tlv(0xa1, attribute))
    signed_data = tlv(0x30, b"".join(field[2] for field in signed[:-1]) + tlv(0x31, signer_info))
    output = tlv(0x30, info[0][2] + tlv(0xa0, signed_data))
    pathlib.Path(sys.argv[4]).write_bytes(output)
else:
    raise ValueError(mode)
