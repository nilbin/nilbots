(() => {
  const skills = [
    {
      className: 'Kestrel',
      signatureName: 'Vector Dash',
      signatureId: 'vector-dash',
      primaryCue: 'signature-motion',
      summary: 'A compact spatial transfer wake on the completed dash.',
      events: [event('Resolve', 'signature-motion', 'primary')],
    },
    {
      className: 'Palisade',
      signatureName: 'Prism Wall',
      signatureId: 'prism-wall',
      primaryCue: 'signature-construct',
      summary: 'A soft energy-panel materialization, reserved for the prism wall.',
      events: [event('Deploy', 'signature-construct', 'primary')],
    },
    {
      className: 'Towline',
      signatureName: 'Tractor Hook',
      signatureId: 'tractor-hook',
      primaryCue: 'signature-tether',
      summary: 'A shared low wind-up followed by the hook’s own taut field connection.',
      events: [
        event('Wind-up', 'attack-windup', 'shared tell'),
        event('Launch', 'signature-tether', 'primary'),
      ],
    },
    {
      className: 'Patchbay',
      signatureName: 'Repair Beam',
      signatureId: 'repair-beam',
      primaryCue: 'signature-repair',
      summary: 'A warm braided transfer on each authoritative medic repair.',
      events: [event('Repair', 'signature-repair', 'primary')],
    },
    {
      className: 'Lantern',
      signatureName: 'Survey Flare',
      signatureId: 'survey-flare',
      primaryCue: 'signature-survey',
      summary: 'A restrained calibration field when the flare lands.',
      events: [event('Land', 'signature-survey', 'primary')],
    },
    {
      className: 'Mortar',
      signatureName: 'Falling Star',
      signatureId: 'falling-star',
      primaryCue: 'signature-falling-star',
      summary: 'One low descent-and-arrival gesture; no extra impact layer.',
      events: [event('Complete', 'signature-falling-star', 'primary')],
    },
    {
      className: 'Minesmith',
      signatureName: 'Trip Node',
      signatureId: 'trip-node',
      primaryCue: 'signature-trip-deploy',
      summary: 'A tiny mechanism at placement and a separate muted snap on trigger.',
      events: [
        event('Deploy', 'signature-trip-deploy', 'primary'),
        event('Trigger', 'signature-trip-trigger', 'secondary'),
      ],
    },
    {
      className: 'Hush',
      signatureName: 'Null Field',
      signatureId: 'null-field',
      primaryCue: 'signature-null',
      summary: 'A dark field lock only when the null zone activates.',
      events: [event('Activate', 'signature-null', 'primary')],
    },
    {
      className: 'Relay',
      signatureName: 'Arc Toss',
      signatureId: 'arc-toss',
      primaryCue: 'signature-arc-toss',
      summary: 'A levitating launch; the later landing keeps the familiar Core drop.',
      events: [
        event('Launch', 'signature-arc-toss', 'primary'),
        event('Land', 'arc-drop', 'shared Core'),
      ],
    },
    {
      className: 'Switchback',
      signatureName: 'Exchange',
      signatureId: 'exchange',
      primaryCue: 'signature-exchange',
      summary: 'A paired spatial fold only when the exchange completes.',
      events: [event('Complete', 'signature-exchange', 'primary')],
    },
    {
      className: 'Longshot',
      signatureName: 'Rail Line',
      signatureId: 'rail-line',
      primaryCue: 'signature-rail-release',
      summary: 'Its own low charge and soft rail discharge replace regular attack audio.',
      events: [
        event('Charge', 'signature-rail-charge', 'secondary'),
        event('Release', 'signature-rail-release', 'primary'),
        event('Hit', 'impact', 'shared armor'),
      ],
    },
    {
      className: 'Mason',
      signatureName: 'Hardlight Block',
      signatureId: 'hardlight-block',
      primaryCue: 'signature-mason',
      summary: 'An earthy ceramic rise and seat, deliberately unlike Prism Wall.',
      events: [event('Build', 'signature-mason', 'primary')],
    },
    {
      className: 'Sunder',
      signatureName: 'Target Paint',
      signatureId: 'target-paint',
      primaryCue: 'signature-target',
      summary: 'A compact acquisition lock when the target is authoritatively acquired.',
      events: [event('Acquire', 'signature-target', 'primary')],
    },
    {
      className: 'Repulsor',
      signatureName: 'Kinetic Burst',
      signatureId: 'kinetic-burst',
      primaryCue: 'signature-kinetic',
      summary: 'A short low pressure release on the resolved local burst.',
      events: [event('Resolve', 'signature-kinetic', 'primary')],
    },
    {
      className: 'Veil',
      signatureName: 'Smoke Canister',
      signatureId: 'smoke-canister',
      primaryCue: 'signature-smoke',
      summary: 'A muted field bloom when the canister lands, not during flight.',
      events: [event('Land', 'signature-smoke', 'primary')],
    },
    {
      className: 'Nest',
      signatureName: 'Sentinel Seed',
      signatureId: 'sentinel-seed',
      primaryCue: 'signature-sentinel-deploy',
      summary: 'A seed unfold on deployment and a tiny low pulse for each real bolt launch.',
      events: [
        event('Deploy', 'signature-sentinel-deploy', 'primary'),
        event('Fire', 'signature-sentinel-fire', 'secondary'),
        event('Hit', 'impact', 'shared armor'),
      ],
    },
  ];

  const comparisons = [
    {
      label: 'CONSTRUCT SPLIT',
      title: 'Palisade → Mason',
      description: 'Energy panels first, earthy ceramic slabs second.',
      steps: [[0, 'signature-construct'], [950, 'signature-mason']],
    },
    {
      label: 'HEALING SPLIT',
      title: 'Healing tile → Patchbay',
      description: 'Shared arena restoration first, medic transfer second.',
      steps: [[0, 'healing'], [1_050, 'signature-repair']],
    },
    {
      label: 'LONGSHOT SENTENCE',
      title: 'Charge → release → hit',
      description: 'The rail owns its tell and release; armor owns contact.',
      steps: [[0, 'signature-rail-charge'], [480, 'signature-rail-release'], [720, 'impact']],
    },
    {
      label: 'TRIP NODE SENTENCE',
      title: 'Deploy → trigger',
      description: 'A quiet placement and a more important route-tax snap.',
      steps: [[0, 'signature-trip-deploy'], [950, 'signature-trip-trigger']],
    },
    {
      label: 'CORE SENTENCE',
      title: 'Arc Toss → landing',
      description: 'A distinct levitating launch returns to the Core family on landing.',
      steps: [[0, 'signature-arc-toss'], [900, 'arc-drop']],
    },
    {
      label: 'SENTINEL SENTENCE',
      title: 'Deploy → fire → hit',
      description: 'The small prop unfolds, emits one pulse per launch, then armor answers.',
      steps: [[0, 'signature-sentinel-deploy'], [900, 'signature-sentinel-fire'], [1_150, 'impact']],
    },
  ];

  const cueGain = {
    'attack-windup': 0.18,
    impact: 0.32,
    healing: 0.28,
    'arc-drop': 0.3,
  };
  const defaultSignatureGain = 0.16;
  const runtimeBase =
    location.protocol === 'file:' || location.pathname.includes('/art/audio/sound-lab/')
      ? '../../../web/src/assets/audio/effects/astral-relay/'
      : './runtime-audio/';

  const skillTemplate = document.querySelector('#skill-template');
  const skillGrid = document.querySelector('[data-skills]');
  const comparisonGrid = document.querySelector('[data-comparisons]');
  const volumeInput = document.querySelector('[data-volume]');
  const volumeOutput = document.querySelector('[data-volume-output]');
  const status = document.querySelector('[data-status]');
  const activeAudio = new Set();
  const timers = new Set();
  let reviewLevel = Number(volumeInput.value);

  renderSkills();
  renderComparisons();

  volumeInput.addEventListener('input', () => {
    reviewLevel = Number(volumeInput.value);
    volumeOutput.value = `${Math.round(reviewLevel * 100)}%`;
    for (const audio of activeAudio) {
      audio.volume = Math.min(1, gainFor(audio.dataset.cue) * reviewLevel);
    }
  });

  document.querySelector('[data-stop]').addEventListener('click', stopAll);
  document.querySelector('[data-tour]').addEventListener('click', () => {
    const steps = skills.map((skill, index) => [index * 920, skill.primaryCue]);
    playSequence('16-skill primary tour', steps);
  });
  window.addEventListener('keydown', (eventValue) => {
    if (eventValue.key === 'Escape') stopAll();
  });

  function event(label, cue, role) {
    return { label, cue, role };
  }

  function renderSkills() {
    skills.forEach((skill, index) => {
      const card = skillTemplate.content.firstElementChild.cloneNode(true);
      card.dataset.signatureId = skill.signatureId;
      card.querySelector('.skill-card__number').textContent = String(index + 1).padStart(2, '0');
      card.querySelector('.skill-card__class').textContent = skill.className;
      card.querySelector('.skill-card__name').textContent = skill.signatureName;
      card.querySelector('.skill-card__summary').textContent = skill.summary;
      const events = card.querySelector('.skill-card__events');
      for (const item of skill.events) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'event-button';
        button.dataset.cue = item.cue;
        button.innerHTML = `<strong>${item.label}</strong><span>${item.role}</span><small>${item.cue}</small>`;
        button.addEventListener('click', () => playCue(item.cue, button));
        events.append(button);
      }
      skillGrid.append(card);
    });
  }

  function renderComparisons() {
    for (const comparison of comparisons) {
      const card = document.createElement('article');
      card.className = 'comparison-card';
      const label = document.createElement('span');
      label.textContent = comparison.label;
      const title = document.createElement('h3');
      title.textContent = comparison.title;
      const description = document.createElement('p');
      description.textContent = comparison.description;
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = 'Play comparison';
      button.addEventListener('click', () =>
        playSequence(comparison.title, comparison.steps),
      );
      card.append(label, title, description, button);
      comparisonGrid.append(card);
    }
  }

  function gainFor(cue) {
    return cueGain[cue] ?? defaultSignatureGain;
  }

  function playCue(cue, sourceButton = null) {
    const audio = new Audio(`${runtimeBase}${cue}.m4a`);
    audio.preload = 'auto';
    audio.dataset.cue = cue;
    audio.volume = Math.min(1, gainFor(cue) * reviewLevel);
    activeAudio.add(audio);
    if (sourceButton) sourceButton.dataset.playing = 'true';
    const cleanup = () => {
      activeAudio.delete(audio);
      if (sourceButton) sourceButton.dataset.playing = 'false';
    };
    audio.addEventListener('ended', cleanup, { once: true });
    audio.addEventListener('error', () => {
      cleanup();
      status.textContent = `Could not load ${cue}. Rebuild the review site or open it from the repository.`;
    }, { once: true });
    const attempt = audio.play();
    if (attempt) {
      attempt.catch(() => {
        cleanup();
        status.textContent = 'Playback was blocked. Tap a cue again to enable audio.';
      });
    }
    status.textContent = `Playing ${cue}`;
  }

  function playSequence(label, steps) {
    stopAll(false);
    status.textContent = `Playing ${label}`;
    for (const [delay, cue] of steps) {
      if (delay === 0) {
        playCue(cue);
        continue;
      }
      const timer = window.setTimeout(() => {
        timers.delete(timer);
        playCue(cue);
      }, delay);
      timers.add(timer);
    }
  }

  function stopAll(announce = true) {
    for (const timer of timers) window.clearTimeout(timer);
    timers.clear();
    for (const audio of activeAudio) {
      audio.pause();
      audio.currentTime = 0;
    }
    activeAudio.clear();
    document.querySelectorAll('[data-playing="true"]').forEach((button) => {
      button.dataset.playing = 'false';
    });
    if (announce) status.textContent = 'Audio stopped.';
  }
})();
