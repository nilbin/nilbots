#!/usr/bin/env python3
"""Generate the breakwater-v1 playbook + layout (nestk-r212hl)."""
import json, hashlib, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
BASE = ROOT / 'arena-bots/arc-relay/tactical-playbook-v1-2026-08-03'
T = json.load(open(BASE / 'playbooks/home-siege-v3.json'))

# ---------------- layout ----------------
layout = {
  "schema": "arc-relay-tactical-layout-v1",
  "layoutId": "counterflow-breakwater-v1",
  "mapId": "arc-relay-threefold-depth-counterflow-01",
  "bindings": [
    {"matchContractFingerprint": "any-composition", "ownReactorSide": "west",
     "transform": "identity", "routeAliases": {}, "formationAliases": {}},
    {"matchContractFingerprint": "any-composition", "ownReactorSide": "east",
     "transform": "rotate-180",
     "routeAliases": {"north-out": "south-out", "south-out": "north-out"},
     "formationAliases": {}},
  ],
  "zones": [
    {"zoneId": "own-home",     "rect": [1, 7, 7, 15]},
    {"zoneId": "own-choke",    "rect": [3, 6, 7, 16]},
    {"zoneId": "own-approach", "rect": [4, 4, 11, 18]},
    {"zoneId": "north-farm",   "rect": [12, 2, 18, 8]},
    {"zoneId": "south-farm",   "rect": [12, 14, 18, 20]},
    {"zoneId": "well-line",    "rect": [12, 2, 18, 20]},
  ],
  "routes": [
    {"routeId": "north-out", "corridorWidth": 2, "waypoints": [[4, 8], [7, 6], [10, 5], [13, 4], [15, 4]]},
    {"routeId": "south-out", "corridorWidth": 2, "waypoints": [[4, 14], [7, 16], [10, 17], [13, 18], [15, 18]]},
    {"routeId": "centre-probe", "corridorWidth": 2, "waypoints": [[5, 11], [9, 11], [12, 11], [15, 11]]},
  ],
  "anchors": [{"anchorId": "home-gate", "position": [5, 11]}],
}

# ---------------- playbook ----------------
def clone(x):
    return json.loads(json.dumps(x))

def bands(*rows):
    return [{"roleId": r, "sector": sec, "offsets": offs} for r, sec, offs in rows]

FULL_COVER = [
    ("anchor", "front",  [[0, -1], [0, 1]]),
    ("hook",   "front",  [[0, 0]]),
    ("runner", "centre", [[1, -2], [1, -1], [1, 0], [1, 1], [1, 2], [2, -1], [2, 0], [2, 1]]),
    ("eyes",   "centre", [[3, 0]]),
    ("medic",  "rear",   [[-1, 0]]),
]

def formation(fid, template_id, pace=None):
    g = clone(next(x for x in T['formations'] if x['formationId'] == template_id))
    g['formationId'] = fid
    g['placementBands'] = bands(*FULL_COVER)
    if pace:
        g['cohesion']['pace'] = pace
    return g

formations = [
    formation('farm-column', 'rush-column', pace='free'),
    formation('choke-wedge', 'rally-wedge'),
    formation('run-escort', 'return-escort', pace='free'),
]

def engagement(src_id, new_id, participants, leash, priorities=None):
    e = clone(next(x for x in T['engagements'] if x['engagementId'] == src_id))
    e['engagementId'] = new_id
    e['participants'] = participants
    e['chaseLeash'] = leash
    if priorities:
        e['targetPriorities'] = priorities
    return e

engagements = [
    engagement('rush-focus', 'run-fight', ['runner', 'eyes'], 2),
]
engagements[0]['signatureCoordination'] = 'control-first'
engagements += [
    engagement('rush-focus', 'front-fight',
               ['anchor', 'medic', 'hook', 'runner', 'eyes'], 2,
               ['enemy-carrier', 'closest-to-anchor', 'lowest-health']),
    engagement('rush-focus', 'hunt-carrier', ['runner', 'eyes'], 4,
               ['enemy-carrier', 'closest-to-anchor', 'lowest-health']),
    engagement('regroup-defense', 'choke-hold', ['anchor', 'medic', 'hook'], 1,
               ['enemy-carrier', 'closest-to-anchor', 'lowest-health']),
]
for e in engagements:
    if e['engagementId'] in ('run-fight', 'front-fight'):
        e['maximumAttackersPerTarget'] = 2
    if e['engagementId'] == 'choke-hold':
        e['signatureCoordination'] = 'support-first' 

def custody(src_id, new_id, wells, carriers, escorts):
    c = clone(next(x for x in T['custodyPolicies'] if x['custodyId'] == src_id))
    c['custodyId'] = new_id
    c['sourceWells'] = wells
    c['authorizedCarrierRoles'] = carriers
    c['escortGroups'] = escorts
    c['safeConversionConditionSetId'] = 'convert-freely'
    return c

incidental = clone(next(x for x in T['custodyPolicies'] if 'incidental' in x['custodyId']))
incidental['safeConversionConditionSetId'] = 'convert-freely'
incidental['authorizedCarrierRoles'] = ['runner', 'anchor', 'medic', 'eyes', 'hook']
incidental['escortGroups'] = ['farm-group']
custodies = [
    custody(T['custodyPolicies'][0]['custodyId'], 'farm-north', ['north'], ['runner'], ['farm-group']),
    custody(T['custodyPolicies'][0]['custodyId'], 'farm-south', ['south'], ['runner'], ['farm-group']),
    incidental,
]

support = clone(T['supportPolicies'][0])
support['supportId'] = 'home-repair'
support['providers'] = ['medic']

profile = lambda pid, gid, prio: {
    "groupId": gid, "localState": "ready", "priority": prio,
    "members": {"kind": "all"}, "stuckRecovery": "repath", "supportId": "",
}

def maneuver(formation, movement, assignments):
    return {"tracks": {"main": {"formationId": formation, "movement": movement,
                                "assignments": assignments}}}

def order(engagement_id, custody_id, profile_id, leash=0):
    return {"arrivalRadius": 1, "completion": "cohesion-arrived",
            "chaseLeash": leash, "engagementId": engagement_id,
            "custodyId": custody_id, "fallbackId": "continue-and-hold-invalid",
            "assignmentProfileId": profile_id}

playbook = {
  "schema": "arc-relay-tactical-playbook-v1",
  "playbookId": "breakwater-v1",
  "auditStatus": {"provisionalEvaluationOnly": True, "playerFacingProductSchema": False},
  "composition": ["palisade", "palisade", "patchbay", "lantern",
                   "nest", "kestrel", "relay", "towline"],
  "layout": {"path": "../layouts/counterflow-breakwater-v1.json",
             "sha256": "FILLED-BELOW"},
  "perspective": "team-relative",
  "memory": clone(T['memory']),
  "arbitration": clone(T['arbitration']),
  "roles": [
    {"roleId": "anchor", "candidateClasses": ["palisade"], "minimum": 1,
     "preferred": 2, "maximum": 2, "carrierPreference": "allow",
     "deathPolicy": "hold-vacancy", "respawnPolicy": "rejoin",
     "overflowRoleId": "runner"},
    {"roleId": "medic", "candidateClasses": ["patchbay"], "minimum": 0,
     "preferred": 1, "maximum": 1, "carrierPreference": "allow",
     "deathPolicy": "hold-vacancy", "respawnPolicy": "rejoin",
     "overflowRoleId": "runner"},
    {"roleId": "eyes", "candidateClasses": ["lantern"], "minimum": 0,
     "preferred": 1, "maximum": 1, "carrierPreference": "allow",
     "deathPolicy": "promote-best", "respawnPolicy": "replace",
     "overflowRoleId": "runner"},
    {"roleId": "hook", "candidateClasses": ["towline"], "minimum": 0,
     "preferred": 1, "maximum": 1, "carrierPreference": "allow",
     "deathPolicy": "hold-vacancy", "respawnPolicy": "rejoin",
     "overflowRoleId": "runner"},
    {"roleId": "runner", "candidateClasses": ["kestrel", "relay", "palisade",
     "patchbay", "lantern", "towline", "nest"], "minimum": 1, "preferred": 3,
     "maximum": 8, "carrierPreference": "prefer",
     "deathPolicy": "promote-best", "respawnPolicy": "replace",
     "overflowRoleId": "runner"},
  ],
  "groups": [
    {"groupId": "home-group", "roleIds": ["anchor", "medic", "hook"],
     "minimum": 1, "preferred": 4, "maximum": 4,
     "membership": {"persistence": "stable-slot", "casualty": "hold-vacancy",
                     "preemption": "never", "overflow": "unassigned"},
     "localStateMachine": {"initialState": "holding", "states": [
         {"stateId": "holding", "minimumTicks": 0, "transitions": []}]}},
    {"groupId": "farm-group", "roleIds": ["runner", "eyes"],
     "minimum": 1, "preferred": 4, "maximum": 8,
     "membership": {"persistence": "stable-slot", "casualty": "promote-role",
                     "preemption": "phase-boundary", "overflow": "declared-role"},
     "localStateMachine": {"initialState": "ready", "states": [
         {"stateId": "ready", "minimumTicks": 0, "transitions": []}]}},
  ],
  "formations": formations,
  "engagements": engagements,
  "supportPolicies": [support],
  "custodyPolicies": custodies,
  "authoring": {
    "kind": "maneuver-catalog",
    "library": {"path": "../library/standard-v1.json", "sha256": "FILLED-BELOW"},
    "parameters": {
      "detect-mass": {"value": 5, "minimum": 3, "maximum": 8},
      "release-mass": {"value": 2, "minimum": 0, "maximum": 4},
      "linger-mass": {"value": 3, "minimum": 2, "maximum": 6},
    },
    "assignmentProfiles": {
      "home-standard": {"groupId": "home-group", "localState": "holding", "priority": 20, "members": {"kind": "all"}, "stuckRecovery": "repath", "supportId": "home-repair"},
      "farm-standard": profile('farm-standard', 'farm-group', 10),
    },
    "standingOrders": {
      "hold-home": {
        "groupId": "home-group", "priority": 25,
        "members": {"kind": "all"},
        "movement": {"kind": "zone", "target": "own-choke",
                      "arrivalRadius": 1, "completion": "cohesion-arrived",
                      "stuckTicks": 8, "stuckRecovery": "reflow",
                      "chaseLeash": 1, "pace": "slowest"},
        "formationId": "choke-wedge", "engagementId": "choke-hold",
        "custodyId": "incidental-delivery", "localState": "holding",
        "fallback": {"onNoPath": "reflow", "onUnderstrength": "continue",
                      "onInvalidTarget": "hold", "phaseId": ""}
      },
    },
    "maneuvers": {
      "contest-line": maneuver("farm-column",
        {"kind": "zone", "target": "well-line", "stuckTicks": 8, "pace": "free"},
        {"front-home": order("front-fight", "incidental-delivery", "home-standard", 2),
         "front-runners": order("run-fight", "farm-north", "farm-standard", 2)}),
      "hold-line": maneuver("choke-wedge",
        {"kind": "zone", "target": "own-home", "stuckTicks": 8, "pace": "slowest"},
        {"line-hold": order("choke-hold", "incidental-delivery", "home-standard", 1),
         "line-runners": order("choke-hold", "incidental-delivery", "farm-standard", 1)}),
      "farm-north-run": maneuver("run-escort",
        {"kind": "route", "target": "north-out", "stuckTicks": 8, "pace": "free"},
        {"north-runner": order("run-fight", "farm-north", "farm-standard", 2)}),
      "hunt-run": maneuver("run-escort",
        {"kind": "zone", "target": "well-line", "stuckTicks": 8, "pace": "free"},
        {"courier-hunter": order("hunt-carrier", "incidental-delivery", "farm-standard", 4)}),
      "farm-south-run": maneuver("run-escort",
        {"kind": "route", "target": "south-out", "stuckTicks": 8, "pace": "free"},
        {"south-runner": order("run-fight", "farm-south", "farm-standard", 2)}),
    },
    "predicates": {
      "approach-mass": {"fact": "remembered-enemies-in-zone",
                         "operator": "at-least",
                         "valueParameter": "detect-mass", "zone": "own-approach"},
      "approach-clear": {"fact": "remembered-enemies-in-zone",
                          "operator": "at-most",
                          "valueParameter": "release-mass", "zone": "own-approach"},
      "approach-lingering": {"fact": "remembered-enemies-in-zone",
                              "operator": "at-least",
                              "valueParameter": "linger-mass",
                              "zone": "own-approach"},
    },
    "conditionSets": {
      "siege-detected": [["approach-mass"]],
      "siege-released": [["approach-clear"]],
      "convert-freely": [["always"]],
      "task-never-done": [["no-live-friendlies"]],
      "courier-visible": [["visible-enemy-carrier"]],
      "courier-lingering": [["visible-enemy-carrier", "approach-lingering"]],
      "courier-gone": [["no-known-enemy-carrier"]],
    },
  },
  "coordination": {
    "initialPhase": "balanced",
    "tasks": [
      {"taskId": "farm-north-task", "priority": 10, "activation": "while-true",
       "preemption": "higher-priority", "participantLoss": "replace",
       "triggerStableTicks": 1, "minimumTicks": 8, "timeoutTicks": 600,
       "cooldownTicks": 2, "minimumPrimaryBodies": 2,
       "eligiblePhases": ["balanced", "fortify"],
       "assignments": [
         {"assignmentId": "north-runner", "orderId": "north-runner",
          "roles": ["runner"], "classes": ["kestrel", "relay", "lantern"],
          "minimum": 1, "preferred": 2, "maximum": 2, "carrier": "allow",
          "distance": {"kind": "anchor", "target": "home-gate"}}],
       "whenConditionSetId": "convert-freely",
       "completeConditionSetId": "task-never-done",
       "failConditionSetId": "task-never-done",
       "reintegration": {"mode": "primary-order", "orderIds": [],
                          "completeConditionSetId": "", "timeoutTicks": 0}},
      {"taskId": "farm-south-task", "priority": 11, "activation": "while-true",
       "preemption": "higher-priority", "participantLoss": "replace",
       "triggerStableTicks": 1, "minimumTicks": 8, "timeoutTicks": 600,
       "cooldownTicks": 2, "minimumPrimaryBodies": 2,
       "eligiblePhases": ["balanced", "fortify"],
       "assignments": [
         {"assignmentId": "south-runner", "orderId": "south-runner",
          "roles": ["runner"], "classes": ["kestrel", "relay", "lantern"],
          "minimum": 1, "preferred": 2, "maximum": 2, "carrier": "allow",
          "distance": {"kind": "anchor", "target": "home-gate"}}],
       "whenConditionSetId": "convert-freely",
       "completeConditionSetId": "task-never-done",
       "failConditionSetId": "task-never-done",
       "reintegration": {"mode": "primary-order", "orderIds": [],
                          "completeConditionSetId": "", "timeoutTicks": 0}},
      {"taskId": "counter-courier-linger", "priority": 6,
       "activation": "while-true",
       "preemption": "higher-priority", "participantLoss": "replace",
       "triggerStableTicks": 1, "minimumTicks": 4, "timeoutTicks": 60,
       "cooldownTicks": 4, "minimumPrimaryBodies": 2,
       "eligiblePhases": ["balanced"],
       "assignments": [
         {"assignmentId": "courier-hunter", "orderId": "courier-hunter",
          "roles": ["runner"], "classes": ["kestrel", "relay"],
          "minimum": 1, "preferred": 2, "maximum": 2, "carrier": "forbid",
          "distance": {"kind": "anchor", "target": "home-gate"}}],
       "whenConditionSetId": "courier-lingering",
       "completeConditionSetId": "courier-gone",
       "failConditionSetId": "task-never-done",
       "reintegration": {"mode": "primary-order", "orderIds": [],
                          "completeConditionSetId": "", "timeoutTicks": 0}},
      {"taskId": "counter-courier", "priority": 5, "activation": "while-true",
       "preemption": "higher-priority", "participantLoss": "replace",
       "triggerStableTicks": 1, "minimumTicks": 4, "timeoutTicks": 60,
       "cooldownTicks": 4, "minimumPrimaryBodies": 2,
       "eligiblePhases": ["fortify"],
       "assignments": [
         {"assignmentId": "courier-hunter", "orderId": "courier-hunter",
          "roles": ["runner"], "classes": ["kestrel", "relay"],
          "minimum": 1, "preferred": 2, "maximum": 2, "carrier": "forbid",
          "distance": {"kind": "anchor", "target": "home-gate"}}],
       "whenConditionSetId": "courier-visible",
       "completeConditionSetId": "courier-gone",
       "failConditionSetId": "task-never-done",
       "reintegration": {"mode": "primary-order", "orderIds": [],
                          "completeConditionSetId": "", "timeoutTicks": 0}},
    ],
    "phases": [
      {"phaseId": "balanced", "minimumTicks": 12, "maneuverId": "contest-line",
       "standingOrderIds": [],
       "transitions": [
         {"priority": 10, "to": "fortify", "cause": "success",
          "minimumPolicy": "respect", "stableTicks": 2,
          "conditionSetId": "siege-detected"}]},
      {"phaseId": "fortify", "minimumTicks": 20, "maneuverId": "hold-line",
       "standingOrderIds": [],
       "transitions": [
         {"priority": 10, "to": "balanced", "cause": "failure",
          "minimumPolicy": "respect", "stableTicks": 12,
          "conditionSetId": "siege-released"}]},
    ],
  },
}

lay_path = BASE / 'layouts/counterflow-breakwater-v1.json'
lay_path.write_text(json.dumps(layout, indent=2) + "\n")
playbook['layout']['sha256'] = hashlib.sha256(lay_path.read_bytes()).hexdigest()
lib_path = BASE / 'library/standard-v1.json'
playbook['authoring']['library']['sha256'] = hashlib.sha256(lib_path.read_bytes()).hexdigest()
pb_path = BASE / 'playbooks/breakwater-v1.json'
pb_path.write_text(json.dumps(playbook, indent=2) + "\n")
print("wrote", lay_path.name, pb_path.name)
