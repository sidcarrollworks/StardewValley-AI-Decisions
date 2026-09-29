"""Post one yes/no (noul) question to a local Laya server and print the answer.

Usage: python smoke_test.py [base_url]     (default http://127.0.0.1:8000)
Set LAYA_API_KEY in the environment if the server requires a bearer token.
Standard library only.
"""
import json
import os
import sys
import urllib.error
import urllib.request

base = (sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:8000").rstrip("/")

body = {
    "state": "Abigail spent the whole day alone in the mines and has not spoken to anyone. "
             "She skipped dinner and went straight to her room.",
    "model": "typed-decisions",
    "questions": {
        "lonely": {"type": "noul", "instructions": "Is Abigail feeling lonely?"},
    },
}

headers = {"Content-Type": "application/json"}
key = os.environ.get("LAYA_API_KEY")
if key:
    headers["Authorization"] = "Bearer " + key

req = urllib.request.Request(base + "/v1/systemone", data=json.dumps(body).encode("utf-8"),
                             headers=headers, method="POST")
try:
    # Generous timeout: the first request after start-up can be slow.
    with urllib.request.urlopen(req, timeout=120) as resp:
        result = json.load(resp)
except urllib.error.HTTPError as e:
    print("HTTP %d: %s" % (e.code, e.read().decode("utf-8", "replace")), file=sys.stderr)
    sys.exit(1)
except urllib.error.URLError as e:
    print("Could not reach %s: %s (is laya-serve running?)" % (base, e.reason), file=sys.stderr)
    sys.exit(1)

answer = result["answers"]["lonely"]
print("P(lonely) = %.4f" % answer["noul"])
print("routing:", json.dumps(result.get("routing")))
