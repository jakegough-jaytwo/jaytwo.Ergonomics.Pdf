#!/bin/sh
# Adds seeds that need external tools to a seed directory written by the harness `seeds` command:
# qpdf encryption variants of every `open` seed that qpdf accepts, and OpenSSL RFC 3161 responses for `tsr`.
# Test-only. Usage: external-seeds.sh SEED_DIR
set -eu

SEEDS=$1
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

# Encryption variants with an empty user password, so the `open` target reaches decryption.
for seed in "$SEEDS"/open/*; do
  name=$(basename "$seed")
  for variant in "40 --" "128 --use-aes=n --" "128 --use-aes=y --" "256 --"; do
    out="$WORK/enc"
    # shellcheck disable=SC2086
    if qpdf --encrypt "" owner $variant "$seed" "$out" 2>/dev/null; then
      digest=$(sha256sum "$out" | cut -c1-64)
      cp "$out" "$SEEDS/open/$digest"
      cp "$out" "$SEEDS/edit/$digest"
    fi
  done
  # Object streams and an xref stream from an independent writer.
  if qpdf --object-streams=generate "$seed" "$WORK/os" 2>/dev/null; then
    digest=$(sha256sum "$WORK/os" | cut -c1-64)
    cp "$WORK/os" "$SEEDS/open/$digest"
    cp "$WORK/os" "$SEEDS/edit/$digest"
  fi
done

# RFC 3161 responses. The tsr input is imprint (32 bytes), nonce length, nonce, mode, body. See FuzzTargets.TimestampResponse.
cd "$WORK"
cat > pki.cnf <<'CNF'
[req]
distinguished_name = dn
prompt = no
[dn]
CN = unused
[v3_ca]
basicConstraints = critical,CA:TRUE
keyUsage = critical,keyCertSign,cRLSign
subjectKeyIdentifier = hash
[v3_tsa]
basicConstraints = critical,CA:FALSE
keyUsage = critical,digitalSignature
extendedKeyUsage = critical,timeStamping
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid
CNF
openssl req -x509 -newkey rsa:2048 -nodes -config pki.cnf -extensions v3_ca -subj "/CN=Fuzz Root" -days 2 -keyout root.key -out root.pem 2>/dev/null
openssl req -new -newkey rsa:2048 -nodes -config pki.cnf -subj "/CN=Fuzz TSA" -keyout tsa.key -out tsa.csr 2>/dev/null
openssl x509 -req -in tsa.csr -CA root.pem -CAkey root.key -set_serial 2 -days 2 -extfile pki.cnf -extensions v3_tsa -out tsa.pem 2>/dev/null
echo 01 > serial
cat > ts.cnf <<CNF
[tsa]
default_tsa = tsa1
[tsa1]
serial = $WORK/serial
signer_cert = $WORK/tsa.pem
signer_key = $WORK/tsa.key
certs = $WORK/root.pem
signer_digest = sha256
default_policy = 1.2.3.4.1
digests = sha256
ess_cert_id_alg = sha256
accuracy = secs:1
CNF

for n in 1 2 3; do
  printf 'signature value %s' "$n" > data
  if [ "$n" = 3 ]; then cert=""; else cert="-cert"; fi
  # shellcheck disable=SC2086
  openssl ts -query -data data -sha256 $cert -out q.tsq 2>/dev/null
  nonce=$(openssl ts -query -in q.tsq -text 2>/dev/null | sed -n 's/^Nonce: 0x\([0-9A-Fa-f]*\).*/\1/p')
  [ $((${#nonce} % 2)) -eq 0 ] || nonce="0$nonce"
  openssl dgst -sha256 -binary data > imprint
  openssl ts -reply -config ts.cnf -queryfile q.tsq -out r.tsr 2>/dev/null
  openssl ts -reply -config ts.cnf -queryfile q.tsq -token_out -out t.tst 2>/dev/null
  python3 - "$SEEDS" "$nonce" <<'PY'
import hashlib, pathlib, sys
seeds, nonce = pathlib.Path(sys.argv[1]), bytes.fromhex(sys.argv[2])
imprint = pathlib.Path("imprint").read_bytes()
for mode, body in ((0, "r.tsr"), (1, "t.tst")):
    data = imprint + bytes([len(nonce)]) + nonce + bytes([mode]) + pathlib.Path(body).read_bytes()
    (seeds / "tsr" / hashlib.sha256(data).hexdigest()).write_bytes(data)
token = pathlib.Path("t.tst").read_bytes()
(seeds / "der" / hashlib.sha256(token).hexdigest()).write_bytes(token)
PY
done
openssl x509 -in tsa.pem -outform DER -out "$SEEDS/der/tsa-cert"
