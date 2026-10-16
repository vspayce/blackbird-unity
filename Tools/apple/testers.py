#!/usr/bin/env python3
"""TestFlight for friends: an external group with a public link, the newest builds in it, testers invited by
email, and the builds sent for Beta App Review. Safe to run again (it skips what's already done).

  python3 Tools/apple/testers.py status
  python3 Tools/apple/testers.py go [email ...]

Settings it needs once (Tools/apple/beta.json, git-ignored): feedback email, review contact name and phone.
"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from asc import call, app_id

HERE = os.path.dirname(os.path.abspath(__file__))
GROUP = 'Friends'
DESCRIPTION = ("The Black Bird: Sherlock Holmes in San Francisco, 1895, on the trail of a jewelled falcon. Walk "
               "Burritt Alley in the fog, examine the scene, question witnesses and piece it together in your Mind "
               "Palace; explore the Mark Hopkins mansion on Nob Hill by night and by day.")
WHATS_NEW = ("Early test. Try Chapter I in Burritt Alley, then use Places (the tab at the top of the screen, or the "
             "View button on a controller) to visit the Hopkins house. Tell us what looks wrong, what's confusing, "
             "and whether you hear the sound.")


def data(d):
    if 'error' in d:
        raise SystemExit(f"API error {d['error']}: {d['body']}")
    return d.get('data', [])


def settings():
    p = os.path.join(HERE, 'beta.json')
    return json.load(open(p)) if os.path.exists(p) else {}


def status(aid):
    for g in data(call('GET', f'/v1/apps/{aid}/betaGroups')):
        a = g['attributes']
        n = len(data(call('GET', f"/v1/betaGroups/{g['id']}/betaTesters?limit=200")))
        print(f"group {a['name']!r} internal={a['isInternalGroup']} publicLink={a.get('publicLink')} testers={n}")
    for l in data(call('GET', f'/v1/apps/{aid}/betaAppLocalizations')):
        a = l['attributes']; print(f"localization {a['locale']} feedbackEmail={a.get('feedbackEmail')} description={'yes' if a.get('description') else 'no'}")
    r = call('GET', f'/v1/apps/{aid}/betaAppReviewDetail').get('data', {}).get('attributes', {})
    print('review contact', {k: r.get(k) for k in ('contactFirstName', 'contactLastName', 'contactPhone', 'contactEmail', 'demoAccountRequired')})
    for b in data(call('GET', f'/v1/builds?filter[app]={aid}&limit=5&include=preReleaseVersion')):
        s = call('GET', f"/v1/builds/{b['id']}/betaAppReviewSubmission").get('data')
        st = s['attributes']['betaReviewState'] if s else 'not submitted'
        print(f"build {b['attributes']['version']} {b['attributes']['processingState']} review={st} id={b['id']}")


def go(aid, emails):
    cfg = settings()
    # the beta description and feedback address, shown to testers
    locs = data(call('GET', f'/v1/apps/{aid}/betaAppLocalizations'))
    attrs = {'description': DESCRIPTION, 'feedbackEmail': cfg['feedbackEmail']}
    if locs:
        call('PATCH', f"/v1/betaAppLocalizations/{locs[0]['id']}", {'data': {'type': 'betaAppLocalizations', 'id': locs[0]['id'], 'attributes': attrs}})
    else:
        call('POST', '/v1/betaAppLocalizations', {'data': {'type': 'betaAppLocalizations', 'attributes': dict(attrs, locale='en-US'),
              'relationships': {'app': {'data': {'type': 'apps', 'id': aid}}}}})
    # who Apple's reviewers can contact
    rd = call('GET', f'/v1/apps/{aid}/betaAppReviewDetail')['data']
    call('PATCH', f"/v1/betaAppReviewDetails/{rd['id']}", {'data': {'type': 'betaAppReviewDetails', 'id': rd['id'], 'attributes': {
        'contactFirstName': cfg['firstName'], 'contactLastName': cfg['lastName'], 'contactPhone': cfg['phone'],
        'contactEmail': cfg['feedbackEmail'], 'demoAccountRequired': False,
        'notes': 'A single-player detective game; no account or sign-in. Controls: touch on iPhone/iPad, a game controller on Apple TV.'}}})
    # the external group, with a public link
    groups = data(call('GET', f'/v1/apps/{aid}/betaGroups'))
    ext = next((g for g in groups if not g['attributes']['isInternalGroup']), None)
    if not ext:
        name = GROUP if not any(g['attributes']['name'] == GROUP for g in groups) else GROUP + ' (external)'
        ext = call('POST', '/v1/betaGroups', {'data': {'type': 'betaGroups', 'attributes': {'name': name, 'publicLinkEnabled': True,
                   'publicLinkLimitEnabled': False, 'feedbackEnabled': True},
                   'relationships': {'app': {'data': {'type': 'apps', 'id': aid}}}}})['data']
        print('made group', ext['attributes']['name'])
    gid = ext['id']
    # the newest valid build of each platform: what to test, into the group, and off to review
    seen = set()
    for b in data(call('GET', f'/v1/builds?filter[app]={aid}&filter[processingState]=VALID&sort=-uploadedDate&limit=10&include=preReleaseVersion')):
        pv = call('GET', f"/v1/builds/{b['id']}/preReleaseVersion")['data']['attributes']['platform']
        if pv in seen: continue
        seen.add(pv)
        bl = data(call('GET', f"/v1/builds/{b['id']}/betaBuildLocalizations"))
        if bl:
            call('PATCH', f"/v1/betaBuildLocalizations/{bl[0]['id']}", {'data': {'type': 'betaBuildLocalizations', 'id': bl[0]['id'], 'attributes': {'whatsNew': WHATS_NEW}}})
        else:
            call('POST', '/v1/betaBuildLocalizations', {'data': {'type': 'betaBuildLocalizations', 'attributes': {'locale': 'en-US', 'whatsNew': WHATS_NEW},
                  'relationships': {'build': {'data': {'type': 'builds', 'id': b['id']}}}}})
        call('POST', f'/v1/betaGroups/{gid}/relationships/builds', {'data': [{'type': 'builds', 'id': b['id']}]})
        if not call('GET', f"/v1/builds/{b['id']}/betaAppReviewSubmission").get('data'):
            r = call('POST', '/v1/betaAppReviewSubmissions', {'data': {'type': 'betaAppReviewSubmissions',
                     'relationships': {'build': {'data': {'type': 'builds', 'id': b['id']}}}}})
            print('review', pv, b['attributes']['version'], r.get('data', {}).get('attributes', r))
    # testers by email
    for e in emails:
        r = call('POST', '/v1/betaTesters', {'data': {'type': 'betaTesters', 'attributes': {'email': e},
                 'relationships': {'betaGroups': {'data': [{'type': 'betaGroups', 'id': gid}]}}}})
        print('tester', e, 'added' if 'data' in r else r.get('body', r)[:300])
    g = call('GET', f'/v1/betaGroups/{gid}')['data']['attributes']
    print('public link:', g.get('publicLink'))


if __name__ == '__main__':
    aid = app_id()
    if len(sys.argv) > 1 and sys.argv[1] == 'go':
        go(aid, sys.argv[2:])
    else:
        status(aid)
