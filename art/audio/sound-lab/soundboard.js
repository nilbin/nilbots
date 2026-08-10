(() => {
  const baseManifest = window.SOUND_LAB_V4_MANIFEST;
  const additionalManifest = window.SOUND_LAB_V5_MANIFEST;
  if (!baseManifest || baseManifest.version !== 4)
    throw new Error('V4 SFX review manifest did not load.');
  if (!additionalManifest || additionalManifest.version !== 5)
    throw new Error('V5 SFX Engine manifest did not load.');
  const manifest = {
    ...baseManifest,
    version: 5,
    packs: [...baseManifest.packs, ...additionalManifest.packs],
  };

  const packTemplate = document.querySelector('#pack-template');
  const cueTemplate = document.querySelector('#cue-template');
  const packsContainer = document.querySelector('[data-packs]');
  const compareGroups = document.querySelector('[data-compare-groups]');
  const compareCues = document.querySelector('[data-compare-cues]');
  const comparePlayers = document.querySelector('[data-compare-players]');
  const coverage = document.querySelector('[data-coverage]');
  const status = document.querySelector('[data-audio-status]');
  const volumeInput = document.querySelector('[data-volume]');
  const volumeLabel = document.querySelector('[data-volume-label]');
  const activeAudio = new Set();
  const timers = new Set();

  const attackSentenceIds = [
    'attack-windup',
    'projectile',
    'attack-fizzle',
    'attack-cancel',
  ];
  const combatConsequenceIds = ['impact', 'destroyed'];
  const otherProposalIds = ['arc-handoff', 'deflect', 'arrival'];
  const groups = [
    {
      id: 'attack',
      label: 'Attack sentence',
      cueIds: [...attackSentenceIds, ...combatConsequenceIds],
    },
    {
      id: 'core',
      label: 'Arc Core loop',
      cueIds: [
        'arc-birth',
        'arc-pickup',
        'arc-steal',
        'arc-drop',
        'arc-bank',
        'arc-pulse',
      ],
    },
    {
      id: 'other',
      label: 'Other additions',
      cueIds: otherProposalIds,
    },
  ];

  const sequences = {
    possession: [
      [0, 'arc-birth'],
      [1_380, 'arc-pickup'],
      [2_180, 'arc-handoff'],
      [3_060, 'arc-steal'],
      [4_100, 'arc-drop'],
      [5_020, 'arc-pickup'],
      [5_850, 'arc-bank'],
      [7_350, 'arc-pulse'],
    ],
    combat: [
      [0, 'attack-windup'],
      [360, 'projectile'],
      [920, 'impact'],
      [1_720, 'attack-windup'],
      [2_080, 'attack-fizzle'],
      [3_220, 'attack-windup'],
      [3_550, 'attack-cancel'],
      [4_400, 'attack-windup'],
      [4_760, 'projectile'],
      [5_360, 'destroyed'],
      [7_080, 'arrival'],
    ],
    full: [
      [0, 'arc-birth'],
      [1_360, 'arc-pickup'],
      [2_130, 'arc-handoff'],
      [3_030, 'attack-windup'],
      [3_390, 'projectile'],
      [3_950, 'impact'],
      [4_720, 'attack-windup'],
      [5_080, 'attack-fizzle'],
      [6_150, 'arc-steal'],
      [7_220, 'arc-drop'],
      [8_150, 'arc-pickup'],
      [8_980, 'arc-bank'],
      [10_500, 'arc-pulse'],
      [13_180, 'attack-windup'],
      [13_520, 'attack-cancel'],
      [14_420, 'destroyed'],
      [16_250, 'arrival'],
    ],
  };

  let volume = Number(volumeInput.value);
  let selectedGroup = 'attack';
  let selectedCue = 'attack-windup';
  let selectedPack = manifest.packs[0];
  let favorite = readFavorite();

  function readFavorite() {
    try {
      return (
        window.localStorage.getItem('nilbots.audio.v5.favorite') ??
        window.localStorage.getItem('nilbots.audio.v4.favorite') ??
        ''
      );
    } catch {
      return '';
    }
  }

  function writeFavorite(value) {
    try {
      if (value) window.localStorage.setItem('nilbots.audio.v5.favorite', value);
      else window.localStorage.removeItem('nilbots.audio.v5.favorite');
    } catch {
      // A file URL or privacy setting can deny storage. Audio review still works.
    }
  }

  function cueById(pack, cueId) {
    return pack.cues.find((cue) => cue.id === cueId);
  }

  function groupById(groupId) {
    return groups.find((group) => group.id === groupId);
  }

  function groupForCue(cueId) {
    return groups.find((group) => group.cueIds.includes(cueId));
  }

  function setStatus(message) {
    status.textContent = message;
  }

  function updateSequencePack() {
    const label = document.querySelector('[data-sequence-pack]');
    if (label)
      label.textContent =
        `Using ${selectedPack.number} · ${selectedPack.label}. Tap a theme above to switch.`;
  }

  function audioKey(pack, cue) {
    return `${pack.id}/${cue.id}`;
  }

  function clearPlayingState(key) {
    document
      .querySelectorAll(`[data-audio-key="${CSS.escape(key)}"]`)
      .forEach((element) => element.classList.remove('is-playing'));
  }

  function stopAll({ announce = true } = {}) {
    for (const timer of timers) window.clearTimeout(timer);
    timers.clear();
    for (const audio of activeAudio) {
      audio.pause();
      audio.currentTime = 0;
    }
    activeAudio.clear();
    document
      .querySelectorAll('.is-playing')
      .forEach((element) => element.classList.remove('is-playing'));
    if (announce) setStatus('Stopped.');
  }

  function play(pack, cue, { overlap = false } = {}) {
    if (!cue) return;
    if (!overlap) stopAll({ announce: false });
    selectedPack = pack;
    updateSequencePack();
    const key = audioKey(pack, cue);
    const audio = new Audio(cue.file);
    audio.preload = 'auto';
    audio.volume = volume;
    activeAudio.add(audio);
    document
      .querySelectorAll(`[data-audio-key="${CSS.escape(key)}"]`)
      .forEach((element) => element.classList.add('is-playing'));

    const finished = () => {
      activeAudio.delete(audio);
      if (![...activeAudio].some((candidate) => candidate.dataset.audioKey === key))
        clearPlayingState(key);
    };
    audio.dataset.audioKey = key;
    audio.addEventListener('ended', finished, { once: true });
    audio.addEventListener(
      'error',
      () => {
        finished();
        setStatus(`Could not load ${pack.label} / ${cue.label}.`);
      },
      { once: true },
    );
    void audio.play().then(
      () => setStatus(`${pack.label} · ${cue.label}`),
      () => {
        finished();
        setStatus('Playback was blocked. Tap a cue once to enable audio.');
      },
    );
  }

  function playSequence(pack, sequenceId, trigger) {
    stopAll({ announce: false });
    selectedPack = pack;
    updateSequencePack();
    trigger?.classList.add('is-playing');
    setStatus(`${pack.label} · ${sequenceId === 'full' ? 'full story' : `${sequenceId} context test`}`);
    const timeline = sequences[sequenceId];
    for (const [delay, cueId] of timeline) {
      const timer = window.setTimeout(() => {
        timers.delete(timer);
        play(pack, cueById(pack, cueId), { overlap: true });
      }, delay);
      timers.add(timer);
    }
    const [lastDelay, lastCueId] = timeline[timeline.length - 1];
    const lastCue = cueById(pack, lastCueId);
    const finishTimer = window.setTimeout(() => {
      timers.delete(finishTimer);
      trigger?.classList.remove('is-playing');
      setStatus(`${pack.label} · sequence complete`);
    }, lastDelay + lastCue.durationSeconds * 1_000 + 120);
    timers.add(finishTimer);
  }

  function renderCompareGroups() {
    compareGroups.replaceChildren();
    for (const group of groups) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'compare-group';
      button.textContent = group.label;
      button.classList.toggle('is-selected', group.id === selectedGroup);
      button.addEventListener('click', () => {
        selectedGroup = group.id;
        if (!group.cueIds.includes(selectedCue)) selectedCue = group.cueIds[0];
        renderComparison();
      });
      compareGroups.append(button);
    }
  }

  function renderCompareCues() {
    compareCues.replaceChildren();
    const referencePack = manifest.packs[0];
    for (const cueId of groupById(selectedGroup).cueIds) {
      const cue = cueById(referencePack, cueId);
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'compare-chip';
      button.textContent = cue.label;
      button.classList.toggle('is-selected', cue.id === selectedCue);
      button.addEventListener('click', () => {
        selectedCue = cue.id;
        renderComparison();
      });
      compareCues.append(button);
    }
  }

  function waveformBars(pack, cue) {
    let state = 0;
    for (const character of `${pack.id}/${cue.id}`)
      state = (Math.imul(state, 31) + character.charCodeAt(0)) >>> 0;
    const fragment = document.createDocumentFragment();
    for (let index = 0; index < 24; index++) {
      state = (Math.imul(state, 1_664_525) + 1_013_904_223) >>> 0;
      const bar = document.createElement('i');
      const shape = Math.sin(((index + 1) / 25) * Math.PI);
      const random = state / 4_294_967_296;
      bar.style.setProperty('--bar-height', `${Math.round(4 + shape * (8 + random * 20))}px`);
      bar.style.setProperty('--bar-delay', `${index * -27}ms`);
      fragment.append(bar);
    }
    return fragment;
  }

  function renderComparePlayers() {
    comparePlayers.replaceChildren();
    for (const pack of manifest.packs) {
      const cue = cueById(pack, selectedCue);
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'compare-player';
      button.style.setProperty('--pack-accent', pack.accent);
      button.dataset.audioKey = audioKey(pack, cue);

      const top = document.createElement('span');
      top.className = 'compare-player__top';
      top.innerHTML = `<span>${pack.number} / ${pack.kicker}</span><i class="compare-player__play" aria-hidden="true"></i>`;
      const name = document.createElement('strong');
      name.textContent = pack.label;
      const detail = document.createElement('small');
      detail.textContent = cue.description;
      const wave = document.createElement('span');
      wave.className = 'compare-player__wave';
      wave.append(waveformBars(pack, cue));
      button.append(top, name, detail, wave);
      button.addEventListener('click', () => play(pack, cue));
      comparePlayers.append(button);
    }
  }

  function renderComparison() {
    renderCompareGroups();
    renderCompareCues();
    renderComparePlayers();
  }

  function appendAxes(container, pack) {
    const labels = {
      luminous: 'Light',
      depth: 'Depth',
      motion: 'Motion',
      diffusion: 'Space',
    };
    for (const [axis, value] of Object.entries(pack.axes)) {
      const element = document.createElement('div');
      element.className = 'axis';
      element.innerHTML =
        `<span>${labels[axis]}</span><b>${value}</b>` +
        `<span class="axis__bar"><i style="--axis-value:${value}%"></i></span>`;
      container.append(element);
    }
  }

  function appendCueButton(grid, pack, cue) {
    const button = cueTemplate.content.firstElementChild.cloneNode(true);
    const key = audioKey(pack, cue);
    button.dataset.audioKey = key;
    button.querySelector('.cue__category').textContent = cue.category;
    button.querySelector('.cue__category').dataset.status = cue.status;
    button.querySelector('.cue__duration').textContent = `${cue.durationSeconds.toFixed(2)}s`;
    button.querySelector('.cue__label').textContent = cue.label;
    button.querySelector('.cue__description').textContent = cue.description;
    button
      .querySelectorAll('.cue__bars b')
      .forEach((bar, index) => bar.style.setProperty('--bar-delay', `${index * -71}ms`));
    button.addEventListener('click', () => play(pack, cue));
    grid.append(button);
  }

  function appendCueGroup(container, pack, title, cueIds) {
    const section = document.createElement('section');
    section.className = 'cue-group';
    const heading = document.createElement('h4');
    heading.textContent = title;
    const grid = document.createElement('div');
    grid.className = 'cue-grid';
    for (const cueId of cueIds) appendCueButton(grid, pack, cueById(pack, cueId));
    section.append(heading, grid);
    container.append(section);
  }

  function renderPacks() {
    packsContainer.replaceChildren();
    for (const pack of manifest.packs) {
      const article = packTemplate.content.firstElementChild.cloneNode(true);
      article.style.setProperty('--pack-accent', pack.accent);
      article.dataset.packId = pack.id;
      article.classList.toggle('is-favorite', pack.id === favorite);
      article.querySelector('.pack__number').textContent = pack.number;
      article.querySelector('.pack__kicker').textContent = pack.kicker;
      article.querySelector('h3').textContent = pack.label;
      article.querySelector('.pack__description').textContent = pack.description;
      article.querySelector('.pack__thesis').textContent = pack.thesis;
      article.querySelector('.pack__listen-for').innerHTML =
        `<strong>LISTEN FOR</strong> · ${pack.watchFor.join(' · ')}`;
      appendAxes(article.querySelector('.axis-grid'), pack);

      const demoButton = article.querySelector('[data-pack-demo]');
      demoButton.addEventListener('click', () => playSequence(pack, 'full', demoButton));
      const pickButton = article.querySelector('[data-pick]');
      pickButton.textContent = pack.id === favorite ? 'Current favorite' : 'Mark favorite';
      pickButton.addEventListener('click', () => {
        favorite = favorite === pack.id ? '' : pack.id;
        writeFavorite(favorite);
        renderPacks();
        setStatus(favorite ? `${pack.label} marked as your current favorite.` : 'Favorite cleared.');
      });

      const cueGroups = article.querySelector('.pack__cue-groups');
      appendCueGroup(
        cueGroups,
        pack,
        'ATTACK SENTENCE · ONE LIVE + THREE PROPOSED',
        attackSentenceIds,
      );
      appendCueGroup(
        cueGroups,
        pack,
        'COMBAT CONSEQUENCES · MAPPED NOW',
        combatConsequenceIds,
      );
      appendCueGroup(
        cueGroups,
        pack,
        'ARC CORE LOOP · MAPPED NOW',
        groupById('core').cueIds,
      );
      appendCueGroup(
        cueGroups,
        pack,
        'OTHER PROPOSED ADDITIONS',
        otherProposalIds,
      );
      packsContainer.append(article);
    }
  }

  function renderCoverage() {
    coverage.replaceChildren();
    const head = document.createElement('div');
    head.className = 'coverage-row coverage-row--head';
    head.innerHTML =
      '<span>#</span><span>cue</span><span>authoritative mapping</span><span>status</span>' +
      manifest.packs.map((pack) => `<span data-label="${pack.number}"></span>`).join('');
    coverage.append(head);
    manifest.packs[0].cues.forEach((cue, index) => {
      const row = document.createElement('div');
      row.className = 'coverage-row';
      row.innerHTML =
        `<span>${String(index + 1).padStart(2, '0')}</span>` +
        `<span class="coverage__label">${cue.label}</span>` +
        `<span class="coverage__mapping">${cue.mapping}</span>` +
        `<span class="coverage__status" data-status="${cue.status}">${cue.status}</span>` +
        manifest.packs.map(() => '<span></span>').join('');
      coverage.append(row);
    });
  }

  volumeInput.addEventListener('input', () => {
    volume = Number(volumeInput.value);
    volumeLabel.textContent = `${Math.round(volume * 100)}%`;
    for (const audio of activeAudio) audio.volume = volume;
  });

  document
    .querySelectorAll('[data-action="stop"]')
    .forEach((button) => button.addEventListener('click', () => stopAll()));

  document.querySelectorAll('[data-sequence]').forEach((button) => {
    button.addEventListener('click', () =>
      playSequence(selectedPack, button.dataset.sequence, button),
    );
  });

  document.querySelectorAll('[data-jump-cue]').forEach((button) => {
    button.addEventListener('click', () => {
      selectedCue = button.dataset.jumpCue;
      selectedGroup = groupForCue(selectedCue).id;
      renderComparison();
      document.querySelector('#compare').scrollIntoView({ behavior: 'smooth', block: 'start' });
      setStatus(`Ready to compare ${cueById(manifest.packs[0], selectedCue).label}.`);
    });
  });

  document.addEventListener('keydown', (event) => {
    if (event.target instanceof HTMLInputElement) return;
    if (event.code === 'Space') {
      event.preventDefault();
      stopAll();
      return;
    }
    const packIndex = Number(event.key) - 1;
    if (packIndex >= 0 && packIndex < manifest.packs.length) {
      const pack = manifest.packs[packIndex];
      play(pack, cueById(pack, selectedCue));
      return;
    }
    if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
      event.preventDefault();
      const cueIds = groupById(selectedGroup).cueIds;
      const direction = event.key === 'ArrowLeft' ? -1 : 1;
      const current = cueIds.indexOf(selectedCue);
      selectedCue = cueIds[(current + direction + cueIds.length) % cueIds.length];
      renderComparison();
    }
  });

  document.addEventListener('visibilitychange', () => {
    if (document.hidden) stopAll({ announce: false });
  });

  renderComparison();
  renderPacks();
  renderCoverage();
  updateSequencePack();
})();
