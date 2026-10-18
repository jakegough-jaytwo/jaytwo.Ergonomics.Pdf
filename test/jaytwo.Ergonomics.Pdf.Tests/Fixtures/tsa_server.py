"""Local RFC 3161 server used by the signing tests.

The request body is a DER TimeStampReq. The response is a DER TimeStampResp
from pyHanko, including a signed time-stamp token. Prints "READY <port>" and
serves until killed.
"""

import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from asn1crypto import cms, keys, pem, tsp, x509
from pyhanko.sign.timestamps.dummy_client import DummyTimeStamper
from pyhanko_certvalidator.registry import SimpleCertificateStore


def load_der(path: str, kind: str):
    data = Path(path).read_bytes()
    if pem.detect(data):
        _, _, data = pem.unarmor(data)
    if kind == "cert":
        return x509.Certificate.load(data)
    return keys.PrivateKeyInfo.load(data)


def main() -> None:
    cert = load_der(sys.argv[1], "cert")
    key = load_der(sys.argv[2], "key")
    store = SimpleCertificateStore()
    store.register(cert)
    stamper = DummyTimeStamper(cert, key, certs_to_embed=store)

    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def do_POST(self) -> None:
            length = int(self.headers.get("Content-Length", "0"))
            request = tsp.TimeStampReq.load(self.rfile.read(length))
            response = stamper.request_tsa_response(request)
            body = response.dump()
            self.send_response(200)
            self.send_header("Content-Type", "application/timestamp-reply")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, format: str, *args) -> None:
            return

    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    print("READY " + str(server.server_address[1]), flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
