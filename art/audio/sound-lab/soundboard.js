(() => {
  const manifest = window.SOUND_LAB_V2_MANIFEST;
  const legacyManifest = window.SOUND_LAB_MANIFEST;
  if (!manifest) throw new Error('V2 sound lab manifest did not load.');

  const packTemplate = document.querySelector('#pack-template');
  const cueTemplate = document.querySelector('#cue-template');
  const packsContainer = document.querySelector('[data-packs]');
  const compareCues = document.querySelector('[data-compare-cues]');
  const comparePlayers = document.querySelector('[data-compare-players]');
  const status = document.querySelector('[data-audio-status]');
  const volumeInput = document.querySelector('[data-volume]');
  const volumeLabel = document.querySelector('[data-volume-label]');
  const legacyArchive = document.querySelector('[data-v1-archive]');
  const fusionPanel = document.querySelector('[data-fusion]');
  const activeAudio = new Set();
  const timers = new Set();
  let volume = Number(volumeInput.value);
  let selectedCue = 'projectile-showcase';
  let favorite = window.localStorage.getItem('nilbots.audio.v2.favorite') ?? '';

  const cueById = (pack, cueId) =>
    pack.cues.find((candidate) => candidate.id === cueId);

  function setStatus(message) {
    status.textContent = message;
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
    if (!overlap) stopAll({ announce: false });
    const key = `${pack.id}/${cue.id}`;
    const audio = new Audio(cue.file);
    audio.volume = volume;
    activeAudio.add(audio);
    document
      .querySelectorAll(`[data-audio-key="${CSS.escape(key)}"]`)
      .forEach((element) => element.classList.add('is-playing'));
    const finished = () => {
      activeAudio.delete(audio);
      clearPlayingState(key);
    };
    audio.addEventListener('ended', finished, { once: true });
    audio.addEventListener('error', () => {
      finished();
      setStatus(`Could not load ${pack.label} / ${cue.label}.`);
    }, { once: true });
    void audio.play().then(
      () => setStatus(`${pack.label} · ${cue.label}`),
      () => {
        finished();
        setStatus('Playback was blocked. Tap a cue once to enable audio.');
      },
    );
  }

  const demoTimeline = [
    [0, 'projectile-showcase'],
    [900, 'armor-impact'],
    [1_950, 'bot-destroyed'],
    [4_050, 'entitlement-unlock'],
  ];

  function playDemo(pack) {
    stopAll({ announce: false });
    setStatus(`${pack.label} · demo sequence`);
    for (const [delay, cueId] of demoTimeline) {
      const timer = window.setTimeout(() => {
        timers.delete(timer);
        const cue = cueById(pack, cueId);
        if (cue) play(pack, cue, { overlap: true });
      }, delay);
      timers.add(timer);
    }
  }

  function renderComparison() {
    compareCues.replaceChildren();
    const referencePack = manifest.packs[0];
    for (const cue of referencePack.cues) {
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

    comparePlayers.replaceChildren();
    for (const pack of manifest.packs) {
      const cue = cueById(pack, selectedCue);
      const button = document.createElement('button');
      const key = `${pack.id}/${cue.id}`;
      button.type = 'button';
      button.className = 'compare-player';
      button.style.setProperty('--pack-accent', pack.accent);
      button.dataset.audioKey = key;
      button.innerHTML = `<span>${pack.number} / ${pack.kicker}</span><strong>${pack.label}</strong>`;
      button.addEventListener('click', () => play(pack, cue));
      comparePlayers.append(button);
    }
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
      article
        .querySelector('[data-pack-demo]')
        .addEventListener('click', () => playDemo(pack));
      const pickButton = article.querySelector('[data-pick]');
      pickButton.textContent =
        pack.id === favorite ? 'Current favorite' : 'Mark as favorite';
      pickButton.addEventListener('click', () => {
        favorite = favorite === pack.id ? '' : pack.id;
        if (favorite)
          window.localStorage.setItem('nilbots.audio.v2.favorite', favorite);
        else window.localStorage.removeItem('nilbots.audio.v2.favorite');
        renderPacks();
        setStatus(
          favorite
            ? `${pack.label} marked as your current favorite.`
            : 'Favorite cleared.',
        );
      });

      const grid = article.querySelector('.cue-grid');
      for (const cue of pack.cues) {
        const button = cueTemplate.content.firstElementChild.cloneNode(true);
        const key = `${pack.id}/${cue.id}`;
        button.dataset.audioKey = key;
        button.style.setProperty('--duration', `${cue.durationSeconds}s`);
        button.querySelector('.cue__category').textContent = cue.category;
        button.querySelector('.cue__duration').textContent =
          `${cue.durationSeconds.toFixed(2)}s`;
        button.querySelector('.cue__label').textContent = cue.label;
        button.querySelector('.cue__description').textContent = cue.description;
        button
          .querySelectorAll('.cue__meter i')
          .forEach((bar, index) =>
            bar.style.setProperty('--delay', `${index * -83}ms`),
          );
        button.addEventListener('click', () => play(pack, cue));
        grid.append(button);
      }
      packsContainer.append(article);
    }
  }

  function renderLegacyArchive() {
    if (!legacyManifest || !legacyArchive) return;
    const label = document.createElement('label');
    label.className = 'archive__select';
    label.innerHTML = '<span>V1 CUE</span>';
    const select = document.createElement('select');
    const referencePack = legacyManifest.packs[0];
    for (const cue of referencePack.cues) {
      const option = document.createElement('option');
      option.value = cue.id;
      option.textContent = cue.label;
      select.append(option);
    }
    label.append(select);

    const players = document.createElement('div');
    players.className = 'archive__players';
    const renderPlayers = () => {
      players.replaceChildren();
      for (const pack of legacyManifest.packs) {
        const cue = cueById(pack, select.value);
        const button = document.createElement('button');
        button.type = 'button';
        button.style.setProperty('--pack-accent', pack.accent);
        button.dataset.audioKey = `${pack.id}/${cue.id}`;
        button.innerHTML =
          `<span>${pack.number}</span><strong>${pack.label}</strong>` +
          '<small>Play V1</small>';
        button.addEventListener('click', () => play(pack, cue));
        players.append(button);
      }
    };
    select.addEventListener('change', renderPlayers);
    legacyArchive.append(label, players);
    renderPlayers();
  }

  function renderFusion() {
    const experiment = manifest.experiments?.[0];
    if (!experiment || !fusionPanel) return;
    const cue = experiment.cue;
    const playButton = fusionPanel.querySelector('[data-fusion-play]');
    playButton.dataset.audioKey = `${experiment.id}/${cue.id}`;
    playButton.addEventListener('click', () => play(experiment, cue));
    fusionPanel.querySelector('[data-fusion-duration]').textContent =
      `${cue.durationSeconds.toFixed(2)}s`;
    fusionPanel.querySelector('[data-fusion-description]').textContent =
      experiment.description;
    fusionPanel.hidden = false;
  }

  volumeInput.addEventListener('input', () => {
    volume = Number(volumeInput.value);
    volumeLabel.textContent = `${Math.round(volume * 100)}%`;
    for (const audio of activeAudio) audio.volume = volume;
  });

  document
    .querySelectorAll('[data-action="stop"]')
    .forEach((button) => button.addEventListener('click', () => stopAll()));

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
    }
  });

  document.addEventListener('visibilitychange', () => {
    if (document.hidden) stopAll({ announce: false });
  });

  renderComparison();
  renderFusion();
  renderPacks();
  renderLegacyArchive();
})();
