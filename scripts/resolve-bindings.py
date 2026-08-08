#!/usr/bin/env python3
"""Re-resolve pairing fingerprints and rewrite recognizer + siege-edition bindings."""
import json, hashlib, pathlib, subprocess, copy, sys

ROOT = pathlib.Path('/Users/sebastian.lind/hobby-projects/nilbots-wt/arc-strategy-ladder')
BASE = ROOT / 'arena-bots/arc-relay/tactical-playbook-v1-2026-08-03'
TAC = 'arena-bots/arc-relay/tactical-playbook-mind-v1'
STK = 'arena-bots/arc-relay/stock-mind-v4'
REC = str(BASE / 'playbooks/siege-recognizer-v1.json')
PAR = str(BASE / 'controls/coordination-parity-baseline.json')
SIE_FROZEN = str(BASE / 'playbooks/home-siege-v3.json')

def fp(bot, opp, s0, s1):
    r = subprocess.run(['dotnet', 'run', '--project', 'src/BotArena.Cli',
        '--no-build', '--', 'experiment', 'arc-relay', '--bot', bot,
        '--opponent', opp, '--sheet0', s0, '--sheet1', s1, '--seed', '1',
        '--loop-profile', 'forward-combat', '--print-contract'],
        capture_output=True, text=True, cwd=ROOT)
    return json.loads(r.stdout)['matchContractFingerprint']

fp_s0 = fp(TAC, TAC, REC, SIE_FROZEN)   # recognizer west vs siege
fp_s1 = fp(TAC, TAC, SIE_FROZEN, REC)   # siege west, recognizer east
fp_p0 = fp(TAC, STK, REC, PAR)          # recognizer west vs parity
fp_p1 = fp(STK, TAC, PAR, REC)          # parity west, recognizer east
print('fps:', fp_s0[:12], fp_s1[:12], fp_p0[:12], fp_p1[:12])

# recognizer layout: west bindings for s0/p0, east (aliased) for s1/p1
lp = BASE / 'layouts/counterflow-siege-recognizer-v1.json'
lay = json.loads(lp.read_text())
west = next(b for b in lay['bindings'] if b['ownReactorSide'] == 'west')
east = next(b for b in lay['bindings'] if b['ownReactorSide'] == 'east')
lay['bindings'] = []
for f in (fp_s0, fp_p0):
    b = copy.deepcopy(west); b['matchContractFingerprint'] = f
    lay['bindings'].append(b)
for f in (fp_s1, fp_p1):
    b = copy.deepcopy(east); b['matchContractFingerprint'] = f
    lay['bindings'].append(b)
lp.write_text(json.dumps(lay, indent=2) + "\n")
sha = hashlib.sha256(lp.read_bytes()).hexdigest()
pp = BASE / 'playbooks/siege-recognizer-v1.json'
pb = json.loads(pp.read_text()); pb['layout']['sha256'] = sha
pp.write_text(json.dumps(pb, indent=2) + "\n")

# siege pairing edition: frozen bindings + east clone for fp_s0, west for fp_s1
slp = BASE / 'layouts/counterflow-home-siege-v3-vs-recognizer.json'
frozen = json.loads((BASE / 'layouts/counterflow-home-siege-v3.json').read_text())
swest = next(b for b in frozen['bindings'] if b['ownReactorSide'] == 'west')
seast = next(b for b in frozen['bindings'] if b['ownReactorSide'] == 'east')
b1 = copy.deepcopy(seast); b1['matchContractFingerprint'] = fp_s0
b2 = copy.deepcopy(swest); b2['matchContractFingerprint'] = fp_s1
frozen['bindings'] += [b1, b2]
frozen['layoutId'] = 'counterflow-home-siege-v3-vs-recognizer'
slp.write_text(json.dumps(frozen, indent=2) + "\n")
ssha = hashlib.sha256(slp.read_bytes()).hexdigest()
spp = BASE / 'playbooks/home-siege-v3-vs-recognizer.json'
spb = json.loads((BASE / 'playbooks/home-siege-v3.json').read_text())
spb['layout'] = {'path': '../layouts/counterflow-home-siege-v3-vs-recognizer.json',
                 'sha256': ssha}
spp.write_text(json.dumps(spb, indent=2) + "\n")
print('bindings resolved and written')
