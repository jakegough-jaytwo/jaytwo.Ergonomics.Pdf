FROM ubuntu:26.04 AS dotnet-sdk

ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="${PATH}:/usr/share/dotnet:/root/.dotnet/tools"

RUN apt-get update \
  && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    libicu-dev \
    make \
  && apt-get clean \
  && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
  && chmod +x /tmp/dotnet-install.sh \
  && /tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_ROOT" \
  && /tmp/dotnet-install.sh --channel 8.0 --install-dir "$DOTNET_ROOT" \
  && /tmp/dotnet-install.sh --channel 6.0 --install-dir "$DOTNET_ROOT" \
  && ln -sf "$DOTNET_ROOT/dotnet" /usr/bin/dotnet \
  && rm /tmp/dotnet-install.sh

FROM dotnet-sdk AS base

# External tools for the interop tests. They are test-only, not runtime dependencies.
# Pkcs11Interop loads the unversioned "libdl", which glibc 2.34+ no longer ships, so link it to libdl.so.2.
# libnss3-tools provides NSS certutil, which builds the trust database for poppler-utils' pdfsig (W-14).
RUN apt-get update \
  && apt-get install -y --no-install-recommends \
    libnss3-tools \
    mupdf-tools \
    openssl \
    poppler-utils \
    python-is-python3 \
    python3 \
    python3-pip \
    qpdf \
    softhsm2 \
  && apt-get clean \
  && rm -rf /var/lib/apt/lists/* \
  && ln -sf libdl.so.2 "/usr/lib/$(uname -m)-linux-gnu/libdl.so"

# The Python pins are shared with tools/test-tools/install.ps1 (D-51).
COPY tools/test-tools/requirements.txt /tmp/test-tools-requirements.txt
RUN pip install --no-cache-dir --break-system-packages -r /tmp/test-tools-requirements.txt \
  && rm /tmp/test-tools-requirements.txt

ENV JAYTWO_PKCS11=/usr/lib/softhsm/libsofthsm2.so

FROM base AS builder
WORKDIR /build
COPY . /build
RUN make deps restore build

# B-18 fuzz campaigns (test-only): SharpFuzz instruments the library and libfuzzer-dotnet drives it.
FROM builder AS fuzz
ARG LIBFUZZER_DOTNET_VERSION=v2025.05.02.0904
ENV LIBFUZZER_DOTNET_VERSION=${LIBFUZZER_DOTNET_VERSION}
RUN dotnet tool install --global SharpFuzz.CommandLine --version 2.3.0 \
  && curl -fsSL -o /usr/local/bin/libfuzzer-dotnet \
    "https://github.com/Metalnem/libfuzzer-dotnet/releases/download/${LIBFUZZER_DOTNET_VERSION}/libfuzzer-dotnet-ubuntu" \
  && chmod +x /usr/local/bin/libfuzzer-dotnet

FROM builder AS test
RUN make test
