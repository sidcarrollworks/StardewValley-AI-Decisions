"""Seekable read-only file over HTTP range requests, for reading remote Parquet footers
and selected row groups without downloading whole files. Responses are untrusted data:
only bytes are returned; pyarrow parses them."""
import os, ssl, time, urllib.request, io

_CTX = ssl.create_default_context(cafile=os.environ.get("SSL_CERT_FILE"))
_OPENER = urllib.request.build_opener(urllib.request.ProxyHandler(), urllib.request.HTTPSHandler(context=_CTX))

STATS = {"requests": 0, "bytes": 0}


def http_range(url, start, end_incl, tries=4):
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers={"Range": f"bytes={start}-{end_incl}"})
            with _OPENER.open(req, timeout=120) as r:
                data = r.read()
            STATS["requests"] += 1
            STATS["bytes"] += len(data)
            return data
        except Exception as e:  # noqa
            if i == tries - 1:
                raise
            time.sleep(2 * (i + 1))


def http_size(url):
    req = urllib.request.Request(url, method="HEAD")
    with _OPENER.open(req, timeout=60) as r:
        return int(r.headers["Content-Length"])


class RangeFile(io.RawIOBase):
    def __init__(self, url, size=None, block=1 << 20):
        self.url = url
        self.size = size if size is not None else http_size(url)
        self.pos = 0
        self.block = block
        self.cache = {}

    def readable(self):
        return True

    def seekable(self):
        return True

    def seek(self, off, whence=0):
        if whence == 0:
            self.pos = off
        elif whence == 1:
            self.pos += off
        else:
            self.pos = self.size + off
        return self.pos

    def tell(self):
        return self.pos

    def _fetch(self, start, end):  # [start, end)
        # big reads go straight through; small ones are cached by block
        if end - start > 4 * self.block:
            return http_range(self.url, start, end - 1)
        out = bytearray()
        b0, b1 = start // self.block, (end - 1) // self.block
        missing = [b for b in range(b0, b1 + 1) if b not in self.cache]
        if missing:
            m0, m1 = missing[0], missing[-1]
            s = m0 * self.block
            e = min(self.size, (m1 + 1) * self.block)
            data = http_range(self.url, s, e - 1)
            for b in range(m0, m1 + 1):
                self.cache[b] = data[(b - m0) * self.block:(b - m0 + 1) * self.block]
        for b in range(b0, b1 + 1):
            out += self.cache[b]
        off = start - b0 * self.block
        return bytes(out[off:off + (end - start)])

    def read(self, n=-1):
        if n is None or n < 0:
            n = self.size - self.pos
        n = min(n, self.size - self.pos)
        if n <= 0:
            return b""
        data = self._fetch(self.pos, self.pos + n)
        self.pos += len(data)
        return data

    def readinto(self, b):
        data = self.read(len(b))
        b[:len(data)] = data
        return len(data)
