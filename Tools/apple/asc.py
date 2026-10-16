#!/usr/bin/env python3
"""App Store Connect from the command line, with the API key in this folder (AuthKey_<id>.p8 + issuer.txt).
No packages needed: the token is signed with openssl.

  python3 Tools/apple/asc.py builds          the app's recent uploads and builds, with their processing state
  python3 Tools/apple/asc.py get /v1/...     any GET, printed as JSON
"""
import base64, glob, json, os, subprocess, sys, time, urllib.error, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
BUNDLE = 'com.vspayce.blackbird'


def _b64(b):
    return base64.urlsafe_b64encode(b).rstrip(b'=')


def token():
    key = sorted(glob.glob(os.path.join(HERE, 'AuthKey_*.p8')))[0]
    kid = os.path.basename(key)[8:-3]
    iss = open(os.path.join(HERE, 'issuer.txt')).read().strip()
    now = int(time.time())
    msg = _b64(json.dumps({'alg': 'ES256', 'kid': kid, 'typ': 'JWT'}).encode()) + b'.' + \
        _b64(json.dumps({'iss': iss, 'iat': now, 'exp': now + 1100, 'aud': 'appstoreconnect-v1'}).encode())
    der = subprocess.run(['openssl', 'dgst', '-sha256', '-sign', key], input=msg, capture_output=True, check=True).stdout
    i = 2
    rl = der[i + 1]; r = der[i + 2:i + 2 + rl]; i += 2 + rl
    sl = der[i + 1]; s = der[i + 2:i + 2 + sl]
    raw = r.lstrip(b'\0').rjust(32, b'\0') + s.lstrip(b'\0').rjust(32, b'\0')
    return (msg + b'.' + _b64(raw)).decode()


def call(method, path, body=None, tries=4):
    for k in range(tries):
        r = _call(method, path, body)
        if not (isinstance(r, dict) and r.get('error', 0) >= 500) or k == tries - 1:
            return r
        time.sleep(3 * (k + 1))         # Apple's API has its moments


def _call(method, path, body=None):
    req = urllib.request.Request('https://api.appstoreconnect.apple.com' + path, method=method,
                                 data=json.dumps(body).encode() if body is not None else None,
                                 headers={'Authorization': 'Bearer ' + token(), 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            txt = r.read().decode()
            return json.loads(txt) if txt else {}
    except (TimeoutError, OSError) as e:
        if isinstance(e, urllib.error.HTTPError): return {'error': e.code, 'body': e.read().decode()[:2000]}
        return {'error': 599, 'body': f'no answer: {e}'}
    except urllib.error.HTTPError as e:
        return {'error': e.code, 'body': e.read().decode()[:2000]}


APP_ID = '6823487655'     # Black Bird: A Holmes Mystery


def app_id():
    return APP_ID


def builds():
    aid = app_id()
    up = call('GET', f'/v1/apps/{aid}/buildUploads?limit=10')
    for u in up.get('data', []):
        a = u['attributes']
        st = a.get('state', {})
        print(f"upload {a.get('platform'):6} {a.get('cfBundleShortVersionString')} ({a.get('cfBundleVersion')})  {st.get('state')}  {a.get('uploadedDate')}")
        for e in st.get('errors', []):
            print('   error', e.get('code'), e.get('description'))
    b = call('GET', f'/v1/builds?filter[app]={aid}&limit=10&include=preReleaseVersion')
    vers = {x['id']: x['attributes'] for x in b.get('included', [])}
    for x in b.get('data', []):
        a = x['attributes']
        pv = vers.get(x['relationships']['preReleaseVersion']['data']['id'], {})
        print(f"build  {pv.get('platform', ''):6} {pv.get('version', '')} ({a.get('version')})  {a.get('processingState')}  id={x['id']}")


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'builds'
    if cmd == 'builds':
        builds()
    elif cmd == 'get':
        print(json.dumps(call('GET', sys.argv[2]), indent=1))
