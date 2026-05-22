(() => {
    const cfg = window.ifsaPartyBoot;
    if (!cfg?.hubPath) {
        console.warn('ifsaPartyBoot eksik.');
        return;
    }

    const lobbyScene = cfg.scene === 'lobby';
    const playScene = cfg.scene === 'play';
    /** Play ekranında cevap alanı ile senkron: yeni turda temizlemek için. */
    let lastAnswerSyncedRoundId;

    /** Son yayınlanan oyun state’i (süre + titreşim tikinde). */
    let latestPlayState = null;

    /** Kalan süre göstergesi interval (500 ms). */
    let roundTimerInterval = null;

    /** Titreşim — tur kimliği değişince sıfırlanır */
    let vibrateRoundKey = null;
    /** Geri sayımda son çalınan saniye (aynı saniyede tek titreşim) */
    let vibrateLastCountdownSecond = null;
    let vibrateTurnNotifiedForRound = false;

    /** Oyun bitti uyarı kutusu ayın turunda bir kez açılsın */
    let playEndFeedbackModalOpened = false;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(cfg.hubPath)
        .withAutomaticReconnect([0, 1500, 4000])
        .build();

    connection.on('lobbyUpdated', (people) => renderPresence(Array.isArray(people) ? people : []));
    connection.on('stateFull', handleStateEnvelope);
    connection.on('chatMessage', appendChatMessage);
    connection.on('kickedFromRoom', async () => {
        toast('Oda kurucusu seni odadan çıkardı.');
        try {
            await connection.stop();
        } catch (_) {
            // ignore
        }

        window.location.assign(`/Room/Join?code=${encodeURIComponent(cfg.roomCode ?? '')}`);
    });

    connection.onreconnecting(() => {
        toast('Bağlantı kopuyor, yeniden bağlanılıyor…');
    });

    connection.onreconnected(async () => {
        try {
            await connection.invoke('JoinRoom', cfg.roomCode, hyphenate(cfg.identity));
            toast('Masaya yeniden bağlandınız.');
        } catch (err) {
            console.warn('JoinRoom after reconnect failed', err);
            toast('Bağlantı yenilendi; durum güncelleniyor…');
        }
    });

    async function invokeHub(method, ...args) {
        try {
            const okPacket = await connection.invoke(method, ...args);
            if (!okPacket?.ok) {
                toast(okPacket?.error || 'Bu adım şimdilik olmadı.');
            }

            return okPacket;
        } catch (error) {
            console.error(method, error);
            toast(`Bağlantıda sorun var: ${error?.message ?? 'bilinmeyen 🫠'}`);
            return null;
        }
    }

    document.addEventListener('DOMContentLoaded', () => {
        if (!window.signalR) {
            toast('SignalR kütüphanesi eksik 📡');
            return;
        }

        wireHostKickDelegation();
        if (lobbyScene) {
            wireLobbyInputs();
            wireRoomChat();
            buildLobbyQrArtifacts();
            startRealtime();
        } else if (playScene) {
            bootstrapPlayInteractions();
            wirePlayEndFeedbackForm();
            startRealtime();
        }
    });

    async function startRealtime() {
        await connection.start();
        const normalizedId = hyphenate(cfg.identity);
        await invokeHub('JoinRoom', cfg.roomCode, normalizedId);
        wireStaticCopyButtons();
    }

    function bootstrapPlayInteractions() {
        bindClick('sendAnswerBtn', sendAnswerClicked);
        bindClick('hostRevealEarlyBtn', () => invokeHub('HostRevealNow', cfg.roomCode, hyphenate(cfg.identity)));
        bindClick('hostSkipQuestionBtn', async () => {
            if (
                !window.confirm(
                    'Bu sorunun cevapları açılmadan sıradaki soruya geçilecek. Emin misin?',
                )
            ) {
                return;
            }

            await invokeHub('HostSkipQuestion', cfg.roomCode, hyphenate(cfg.identity));
        });

        bindClick('hostNextRoundBtn', () => invokeHub('HostNextQuestion', cfg.roomCode, hyphenate(cfg.identity)));

        bindClick('hostFinishGameCollectBtn', confirmHostFinishGame);
        bindClick('hostFinishGameRevealBtn', confirmHostFinishGame);

        wireRoomChat();
        wireVibrateToggle();
        setNickBadge(cfg.nickname);
    }

    async function confirmHostFinishGame() {
        if (
            !window.confirm(
                'Oyun bitecek ve tüm oyuncuların ekranında özet gösterilecek. Onaylıyor musun? (Tamam=Evet)',
            )
        ) {
            return;
        }

        await invokeHub('HostFinishGame', cfg.roomCode, hyphenate(cfg.identity));
    }

    async function sendChatClicked() {
        const inp = document.getElementById('chatInput');
        if (!inp) {
            return;
        }

        const raw = inp.value.trim();
        if (!raw) {
            return;
        }

        const res = await invokeHub('SendChat', cfg.roomCode, hyphenate(cfg.identity), raw);
        if (res?.ok) {
            inp.value = '';
        }
    }

    function wireRoomChat() {
        bindClick('chatSendBtn', sendChatClicked);
        const inp = document.getElementById('chatInput');
        if (!inp) {
            return;
        }

        inp.addEventListener('keydown', (evt) => {
            if (evt.key === 'Enter' && !evt.shiftKey) {
                evt.preventDefault();
                sendChatClicked();
            }
        });
    }

    function appendChatMessage(payload) {
        const holder = document.getElementById('chatMessages');
        if (!holder || !payload) {
            return;
        }

        const nickRaw = payload.nickname ?? '?';
        const textRaw = payload.text ?? '';
        const timeLabel = payload.sentAtUtc ? new Date(payload.sentAtUtc).toLocaleTimeString() : '';

        const nick = encodeText(nickRaw);
        const txt = encodeText(textRaw);
        const timeSafe = encodeText(timeLabel);

        const line = document.createElement('div');
        line.className = 'party-chat-line';
        line.innerHTML = `<div class="d-flex justify-content-between align-items-baseline gap-2"><span class="party-chat-nick">${nick}</span><span class="party-chat-time">${timeSafe}</span></div><div class="party-chat-text">${txt}</div>`;

        holder.appendChild(line);
        while (holder.children.length > 150) {
            holder.removeChild(holder.firstChild);
        }

        holder.scrollTop = holder.scrollHeight;
    }

    function wireVibrateToggle() {
        const chk = document.getElementById('ifsVibrateToggle');
        if (!chk) {
            return;
        }

        try {
            chk.checked = localStorage.getItem('ifsVibrateAlerts') !== '0';
        } catch (_) {
            chk.checked = true;
        }

        chk.addEventListener('change', () => {
            try {
                localStorage.setItem('ifsVibrateAlerts', chk.checked ? '1' : '0');
            } catch (_) {
                /* ignore */
            }
        });
    }

    function wirePlayEndFeedbackForm() {
        const form = document.getElementById('playEndFeedbackForm');
        if (!form || !cfg.playFeedbackUrl) {
            return;
        }

        form.addEventListener('submit', async (ev) => {
            ev.preventDefault();
            const btn = document.getElementById('playEndFeedbackSendBtn');
            if (btn) {
                btn.disabled = true;
            }

            try {
                const params = new URLSearchParams(new FormData(form));
                const res = await fetch(cfg.playFeedbackUrl, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded',
                        Accept: 'application/json',
                    },
                    body: params.toString(),
                });

                let data = {};
                try {
                    data = await res.json();
                } catch (_) {
                    /* ignore */
                }

                if (res.ok && data.ok) {
                    toast('Teşekkürler — geri bildirimin iletildi 🙏');
                    const modalEl = document.getElementById('feedbackAfterGameModal');
                    if (modalEl && window.bootstrap?.Modal) {
                        const inst = window.bootstrap.Modal.getInstance(modalEl);
                        if (inst) {
                            inst.hide();
                        }
                    }
                } else {
                    toast(data.error ?? 'Gönderilemedi — sonra tekrar dene.');
                }
            } catch (_) {
                toast('Bağlantı hatası.');
            }

            if (btn) {
                btn.disabled = false;
            }
        });
    }

    function maybeOpenPlayEndFeedbackModal() {
        if (!playScene || playEndFeedbackModalOpened || !cfg.playFeedbackUrl) {
            return;
        }

        const modalEl = document.getElementById('feedbackAfterGameModal');
        if (!modalEl || typeof window.bootstrap === 'undefined') {
            return;
        }

        playEndFeedbackModalOpened = true;
        const form = document.getElementById('playEndFeedbackForm');
        if (form) {
            form.reset();
            const ta = document.getElementById('feedbackDeveloperMsg');
            if (ta) {
                ta.value = '';
            }
        }

        window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
    }

    function wireLobbyInputs() {
        bindClick('inviteCopyBtn', copyInviteInputs);
        bindClick('nicknameSaveBtn', async () =>
            invokeHub('SetNickname', cfg.roomCode, hyphenate(cfg.identity), normalizeNickname(getInputValue('lobbyNickname')), null));

        bindChange('packageSelect', async () => {
            const id = Number.parseInt(getInputValue('packageSelect'), 10);
            await invokeHub('HostSelectPackage', cfg.roomCode, hyphenate(cfg.identity), id);
        });

        bindClick('startGameBtn', async () => invokeHub('HostStart', cfg.roomCode, hyphenate(cfg.identity)));

        if (cfg.selectedPackageId) {
            setSelectValueSafe('packageSelect', cfg.selectedPackageId);
        }

        applyInputDefaults();
    }

    function applyInputDefaults() {
        const nicknameField = byId('lobbyNickname');
        if (!nicknameField) {
            return;
        }

        if (cfg.nickname) {
            nicknameField.value = cfg.nickname;
        }
    }

    function buildLobbyQrArtifacts() {
        if (cfg.inviteUrl) {
            buildQr(byId('inviteQr'), cfg.inviteUrl);
        }

        if (!cfg.isHost) {
            buildQr(byId('joinQrPassive'), cfg.inviteUrl);
        }
    }

    function wireStaticCopyButtons() {
        bindClick('inviteCopyFloating', () => clipboardCopy(cfg.inviteUrl || ''));
    }

    async function sendAnswerClicked() {
        hideFeedback();
        const textArea = document.getElementById('answerField');
        if (textArea?.disabled) {
            return;
        }

        const text = (textArea?.value || '').trim();
        if (!text) {
            toast('Önce kısa bir cevap yaz.');
            return;
        }

        const res = await invokeHub('SubmitAnswer', cfg.roomCode, hyphenate(cfg.identity), text);
        if (res?.ok) {
            toast('Cevap gönderildi.');
            setAnswerComposerLocked(true);
        }
    }

    function normalizeGameStatePayload(state) {
        if (!state || typeof state !== 'object') {
            return state;
        }
        if (state.phase == null && state.Phase != null) {
            state.phase = state.Phase;
        }
        if (state.roomCode == null && state.RoomCode != null) {
            state.roomCode = state.RoomCode;
        }
        if (state.roundEndsUtc == null && state.RoundEndsUtc != null) {
            state.roundEndsUtc = state.RoundEndsUtc;
        }
        if ((state.questionText === undefined || state.questionText === null) && state.QuestionText != null) {
            state.questionText = state.QuestionText;
        }
        if (
            (!Array.isArray(state.shuffledCards) || state.shuffledCards.length === 0) &&
            Array.isArray(state.ShuffledCards)
        ) {
            state.shuffledCards = state.ShuffledCards;
        }
        return state;
    }

    function handleStateEnvelope(state) {
        normalizeGameStatePayload(state);
        if (!state) {
            return;
        }

        if (lobbyScene && state.phase && state.phase !== 'Lobby') {
            // MVC varsayılan rotası üçüncü segmenti `id` adıyla bağlar; `code` parametresi için sorgu dizesi gerekli.
            window.location.assign(`/Room/Play?code=${encodeURIComponent(state.roomCode)}`);
            return;
        }

        if (playScene && state.phase === 'Lobby') {
            window.location.assign(`/Room/Lobby?code=${encodeURIComponent(state.roomCode)}`);
            return;
        }

        hydratePlayScreens(state);

        if (lobbyScene) {
            renderPresence(state.people || []);
        }

        if (playScene) {
            updatePlayHostKickPanel(state);
        }
    }

    /** Host atma düğmeleri (delegasyon; tek bağlama). */
    function wireHostKickDelegation() {
        if (!cfg.isHost) {
            return;
        }

        if (document.documentElement.dataset.ifsHostKickBound === '1') {
            return;
        }

        document.documentElement.dataset.ifsHostKickBound = '1';
        document.addEventListener('click', async (evt) => {
            const btn = evt.target.closest('[data-action="host-kick-member"]');
            if (!btn) {
                return;
            }

            if (!cfg.isHost) {
                return;
            }

            const rawId = btn.getAttribute('data-member-id');
            if (
                !rawId ||
                comparableMemberGuid(rawId) === comparableMemberGuid(cfg.identity)
            ) {
                return;
            }

            if (!window.confirm('Bu oyuncuyu odadan çıkarmak istiyor musun?')) {
                return;
            }

            evt.preventDefault();
            await invokeHub('HostKickMember', cfg.roomCode, hyphenate(cfg.identity), hyphenate(rawId));
        });
    }

    function hydratePlayScreens(state) {
        if (playScene) {
            latestPlayState = state;
        }

        setNickBadge(state.yourNickname);
        if (state.phase !== 'Finished') {
            playEndFeedbackModalOpened = false;
        }

        toggleSection('finishedPanel', state.phase === 'Finished');
        toggleSection('collectPanel', state.phase === 'CollectingAnswers');
        toggleSection('revealPanel', state.phase === 'Revealed');
        toggleSection('hostRevealPanel', !!(state.phase === 'CollectingAnswers' && state.youAreHost));
        toggleSection('hostNextPanel', !!(state.phase === 'Revealed' && state.youAreHost));

        if (state.phase === 'CollectingAnswers' || state.phase === 'Revealed') {
            setQuestion(state.questionText);
        }

        if (state.phase === 'Finished') {
            renderFinishedGameSummary(state);
            maybeOpenPlayEndFeedbackModal();
        }

        if (state.phase === 'CollectingAnswers') {
            const rid = state.currentRoundId ?? null;
            if (rid != null && rid !== lastAnswerSyncedRoundId) {
                const answerEl = document.getElementById('answerField');
                if (answerEl) {
                    answerEl.value = '';
                }

                lastAnswerSyncedRoundId = rid;
            }

            syncAnswerComposerLockedFromState(state);
            updateAnswerProgressIndicator(state);
            updateAnswerWaitingLine(state);
            syncVibrateRoundForState(state);
            maintainRoundTimerForState(state);
            evaluateVibrationCues(state);
        } else {
            setAnswerComposerLocked(false);
            hideAnswerProgressIndicator();
            hideRoundTimerUi();
        }

        if (state.phase === 'Revealed' && Array.isArray(state.shuffledCards)) {
            animateDeck(state.shuffledCards);
        }
    }

    function fmtSecondsTr(seconds) {
        const n = Number(seconds);
        if (!Number.isFinite(n)) {
            return '—';
        }

        return `${new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 0, maximumFractionDigits: 1 }).format(n)} sn`;
    }

    function renderFinishedGameSummary(state) {
        const s = state.gameFinishedSummary ?? state.GameFinishedSummary;
        const lineFriends = document.getElementById('finishedLineFriends');
        const lineRounds = document.getElementById('finishedLineRounds');
        const fastOl = document.getElementById('finishedFastList');
        const slowOl = document.getElementById('finishedSlowList');
        const fn = document.getElementById('finishedRankFootnote');
        if (!lineFriends || !lineRounds || !fastOl || !slowOl || !fn) {
            return;
        }

        if (!s) {
            lineFriends.textContent = '';
            lineRounds.textContent = 'Özet yüklenemedi.';
            fastOl.innerHTML = '';
            slowOl.innerHTML = '';
            fn.classList.add('d-none');
            return;
        }

        const friends = Number(s.friendCount ?? 0);
        const minutes = Number(s.durationMinutes ?? 1);
        const roundsAsked = Number(s.roundsAnsweredCount ?? 0);
        const totalAns = Number(s.totalAnswersCount ?? 0);

        lineFriends.textContent = `Toplam ${friends} arkadaşla yaklaşık ${minutes} dakika keyifli bir vakit geçirdiniz.`;
        lineRounds.textContent = `${roundsAsked} soru turunda toplam ${totalAns} cevap yazıldı.`;

        fastOl.innerHTML = '';
        slowOl.innerHTML = '';

        const fast = Array.isArray(s.fastestThree) ? s.fastestThree : [];
        const slow = Array.isArray(s.slowestThree) ? s.slowestThree : [];

        const attachRows = (ol, rows) => {
            rows.forEach((entry) => {
                const nick = String(entry?.nickname ?? '?');
                const avg = fmtSecondsTr(entry?.averageAnswerSecondsRounded);
                const li = document.createElement('li');
                li.textContent = `${nick} — ort. ${avg}`;
                ol.appendChild(li);
            });
        };

        const hasRanking = fast.length > 0 || slow.length > 0;
        if (!hasRanking) {
            fastOl.innerHTML = `<li class="text-white-50">Kimse zaman damgalı cevap bırakmadıysa veya veri eksikse sıralama çıkmaz.</li>`;
            slowOl.innerHTML = `<li class="text-white-50">—</li>`;
            fn.classList.add('d-none');
            return;
        }

        attachRows(fastOl, fast);
        attachRows(slowOl, slow);

        fn.textContent =
            'Hız sıralaması; her tur başladıktan sonra cevap yazdığınız sürenin takma adına göre ortalaması ile hesaplandı.';
        fn.classList.remove('d-none');
    }

    function updatePlayHostKickPanel(state) {
        const panel = document.getElementById('playHostKickPanel');
        const list = document.getElementById('playHostKickList');
        if (!panel || !list) {
            return;
        }

        const inPlay = state.phase === 'CollectingAnswers' || state.phase === 'Revealed';
        if (!cfg.isHost || !inPlay) {
            panel.style.display = 'none';
            list.innerHTML = '';
            return;
        }

        panel.style.display = '';
        mountKickableGuests(list, Array.isArray(state.people) ? state.people : []);
    }

    /** Lobide liste ve sayaç; oyunda yalnızca `updatePlayHostKickPanel` kullanılır. */
    function mountKickableGuests(container, entries) {
        if (!container) {
            return;
        }

        container.innerHTML = '';
        entries.forEach((guest) => {
            addKickableGuestRow(container, guest);
        });
    }

    function guestMemberGuidString(guest) {
        const v = guest.publicId ?? guest.PublicId ?? guest.publicID;
        return v !== undefined && v !== null ? String(v) : '';
    }

    function addKickableGuestRow(container, guest) {
        const row = document.createElement('div');
        row.className =
            'd-flex flex-column flex-md-row gap-2 justify-content-between align-items-md-center glass-chip rounded-pill px-4 py-3 text-white mb-2';
        const nickname = encodeText(guest.nickname ?? 'Oyuncu');
        const vibe = guest.isHost
            ? '☆ Kurucu'
            : guest.isConnected
                ? '🟢 masada bağlı'
                : '💤 çevrimdışı';

        let kickMarkup = `<span class="small">${vibe}</span>`;
        if (cfg.isHost && guest.isHost === false) {
            const midRaw = encodeAttr(guestMemberGuidString(guest));
            kickMarkup +=
                `<button type="button" class="btn btn-sm btn-outline-danger rounded-pill" data-action="host-kick-member" data-member-id="${midRaw}">Masadan çıkar</button>`;
        }

        row.innerHTML = `<span class="fw-semibold">${nickname}</span><div class="d-flex gap-2 align-items-center">${kickMarkup}</div>`;
        container.appendChild(row);
    }

    function encodeAttr(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;');
    }

    function comparableMemberGuid(raw) {
        return String(raw ?? '')
            .replace(/-/g, '')
            .toLowerCase()
            .trim();
    }

    function syncAnswerComposerLockedFromState(state) {
        const locked = Boolean(state.alreadySubmittedAnswerThisRound);
        setAnswerComposerLocked(locked);
    }

    function hideAnswerProgressIndicator() {
        const line = document.getElementById('answerProgressLine');
        if (line) {
            line.style.display = 'none';
        }

        hideAnswerWaitingLine();
    }

    /** Sunucudan gelir: cevapsız oyuncular 1–3 kişiyse takma ad listesi */
    function normalizeWaitingNicknames(raw) {
        if (!Array.isArray(raw)) {
            return [];
        }

        return raw.map((x) => String(x ?? '').trim()).filter((s) => s.length > 0);
    }

    function formatWaitingAnswerPhrase(names) {
        if (!names?.length || names.length > 3) {
            return '';
        }

        if (names.length === 1) {
            return `${names[0]} hâlâ cevap vermedi.`;
        }

        if (names.length === 2) {
            return `${names[0]} ve ${names[1]} hâlâ cevap vermedi.`;
        }

        return `${names[0]}, ${names[1]} ve ${names[2]} hâlâ cevap vermedi.`;
    }

    function hideAnswerWaitingLine() {
        const el = document.getElementById('answerWaitingLine');
        if (el) {
            el.style.display = 'none';
            el.textContent = '';
        }
    }

    function parseRoundEndsMs(state) {
        const raw = state.roundEndsUtc ?? state.RoundEndsUtc;
        if (raw == null) {
            return null;
        }

        if (typeof raw === 'number' && Number.isFinite(raw)) {
            return raw > 10_000_000_000 ? raw : raw * 1000;
        }

        if (typeof raw === 'object' && raw instanceof Date) {
            const t = raw.getTime();
            return Number.isFinite(t) ? t : null;
        }

        if (typeof raw === 'string') {
            const t = raw.trim();

            let ms;

            if (/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}/.test(t)) {
                ms = Date.parse(`${t.replace(' ', 'T')}Z`);
            }

            const probablyUtcSansZone =
                /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/.test(t);

            if (probablyUtcSansZone) {
                const msUtc = Date.parse(`${t}Z`);
                if (Number.isFinite(msUtc)) {
                    ms = msUtc;
                }
            }

            if (!Number.isFinite(ms)) {
                ms = Date.parse(t);
            }

            return Number.isFinite(ms) ? ms : null;
        }

        return null;
    }

    /** Kalan süre saniye (yüksek uyarı); tavan yoksa null. */
    function secondsRemainingToEnd(endMs) {
        if (endMs == null) {
            return null;
        }

        const secLeft = Math.max(0, Math.ceil((endMs - Date.now()) / 1000));
        return secLeft;
    }

    function formatClockRemain(secLeft) {
        if (secLeft == null) {
            return '—';
        }

        const m = Math.floor(secLeft / 60);
        const s = secLeft % 60;
        return `${String(m)}:${String(s).padStart(2, '0')}`;
    }

    function timerSecondsConfiguredValue(state) {
        const raw = state.timerSecondsConfigured ?? state.TimerSecondsConfigured;
        const n = Number(raw);
        return Number.isFinite(n) ? n : 0;
    }

    function hideRoundTimerUi() {
        if (roundTimerInterval != null) {
            window.clearInterval(roundTimerInterval);
            roundTimerInterval = null;
        }

        const panel = document.getElementById('roundTimerPanel');
        if (panel) {
            panel.style.display = 'none';
        }

        const clock = document.getElementById('roundTimerClock');
        if (clock) {
            clock.textContent = '—';
            clock.classList.add('text-warning');
            clock.classList.remove('text-danger');
        }

        const fill = document.getElementById('roundTimerFill');
        if (fill) {
            fill.style.width = '100%';
            fill.classList.remove('party-round-timer-fill--critical');
        }

        const track = document.getElementById('roundTimerTrack');
        if (track) {
            track.style.display = 'none';
        }
    }

    function ensureRoundTimerTicker() {
        if (roundTimerInterval != null || !playScene) {
            return;
        }

        roundTimerInterval = window.setInterval(() => {
            tickRoundTimerUi(latestPlayState);
            if (latestPlayState) {
                evaluateVibrationCues(latestPlayState);
            }
        }, 500);
    }

    function maintainRoundTimerForState(state) {
        if (!playScene || state.phase !== 'CollectingAnswers') {
            return;
        }

        const panel = document.getElementById('roundTimerPanel');
        if (!panel) {
            return;
        }

        const endMs = parseRoundEndsMs(state);
        if (!endMs) {
            hideRoundTimerUi();
            return;
        }

        panel.style.display = '';
        const track = document.getElementById('roundTimerTrack');
        const dur = timerSecondsConfiguredValue(state);
        if (track && dur > 0) {
            track.style.display = '';
        } else if (track) {
            track.style.display = 'none';
        }

        ensureRoundTimerTicker();
        tickRoundTimerUi(state);
    }

    function tickRoundTimerUi(state) {
        if (!playScene || !state || state.phase !== 'CollectingAnswers') {
            return;
        }

        const endMs = parseRoundEndsMs(state);
        const clock = document.getElementById('roundTimerClock');
        const fill = document.getElementById('roundTimerFill');

        if (!clock) {
            return;
        }

        if (!endMs) {
            clock.textContent = '—';
            return;
        }

        const secLeft = secondsRemainingToEnd(endMs);
        if (secLeft === null) {
            return;
        }

        clock.textContent = formatClockRemain(secLeft);

        clock.classList.remove('text-warning', 'text-danger');
        if (secLeft <= 3) {
            clock.classList.add('text-danger');
        } else if (secLeft <= 10) {
            clock.classList.add('text-warning');
        } else {
            clock.classList.add('text-warning');
        }

        const dur = timerSecondsConfiguredValue(state);
        if (fill) {
            if (dur > 0) {
                const ratio = dur > 0 ? Math.min(1, Math.max(0, secLeft / dur)) : 1;
                fill.style.width = `${ratio * 100}%`;
                fill.classList.toggle('party-round-timer-fill--critical', secLeft <= 3);
            } else {
                fill.style.width = '100%';
                fill.classList.remove('party-round-timer-fill--critical');
            }
        }

        if (secLeft <= 0) {
            clock.textContent = formatClockRemain(0);
            clock.classList.remove('text-warning');
            clock.classList.add('text-danger');
            const durZero = timerSecondsConfiguredValue(state);
            if (fill && durZero > 0) {
                fill.style.width = '0%';
                fill.classList.add('party-round-timer-fill--critical');
            }
        }
    }

    function syncVibrateRoundForState(state) {
        const rid = state.currentRoundId ?? null;
        if (rid !== vibrateRoundKey) {
            vibrateRoundKey = rid;
            vibrateLastCountdownSecond = null;
            vibrateTurnNotifiedForRound = false;
        }
    }

    function vibrateAllowed() {
        try {
            if (localStorage.getItem('ifsVibrateAlerts') === '0') {
                return false;
            }
        } catch (_) {
            /* ignore */
        }

        const chk = document.getElementById('ifsVibrateToggle');
        return !chk || chk.checked;
    }

    function pulseShake(elementIds) {
        if (typeof window.matchMedia === 'function' && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            return;
        }

        const ids = Array.isArray(elementIds) ? elementIds : [elementIds];
        window.clearTimeout(window.__IFS_SHAKE_T);
        const els = ids.map((id) => document.getElementById(id)).filter(Boolean);
        els.forEach((el) => {
            el.classList.remove('ifs-shake-soft');
            void el.offsetWidth;
            el.classList.add('ifs-shake-soft');
        });
        if (els.length) {
            window.__IFS_SHAKE_T = window.setTimeout(() => {
                els.forEach((el) => el.classList.remove('ifs-shake-soft'));
            }, 500);
        }
    }

    function triggerHaptic(kind) {
        if (!vibrateAllowed()) {
            return;
        }

        const shakeTargets = ['ifsSnack', 'collectPanel', 'answerField', 'questionTitle'];
        pulseShake(shakeTargets);

        if (navigator.vibrate) {
            /** Uzun dizi bazı cihazlarda kısaltılır; yine de belirgin “alarm” etkisi hedeflenir. */
            let pattern;
            if (kind === 'urgent') {
                pattern = [140, 45, 150, 45, 160, 50, 180, 55, 200, 60, 220, 55, 200, 65, 250];
            } else if (kind === 'warn') {
                pattern = [115, 50, 125, 50, 135, 55, 120, 50, 150];
            } else {
                pattern = [100, 40, 120, 40, 100, 40, 130, 50, 150];
            }
            navigator.vibrate(pattern);

            window.clearTimeout(window.__IFS_SHAKE_BURST);
            window.__IFS_SHAKE_BURST = window.setTimeout(
                () => pulseShake(shakeTargets),
                kind === 'urgent' ? 140 : kind === 'warn' ? 190 : 230,
            );

            window.clearTimeout(window.__IFS_VIB_BURST);
            window.__IFS_VIB_BURST = window.setTimeout(() => {
                if (!vibrateAllowed() || typeof navigator.vibrate !== 'function') {
                    return;
                }
                let extra;
                if (kind === 'urgent') {
                    extra = [155, 48, 195, 55, 230, 60, 260];
                } else if (kind === 'warn') {
                    extra = [95, 48, 115, 52, 135, 55, 120];
                } else {
                    extra = [95, 42, 115, 48, 95, 42, 125, 52, 140];
                }
                navigator.vibrate(extra);
            }, kind === 'urgent' ? 250 : kind === 'warn' ? 310 : 280);
        }
    }

    function evaluateVibrationCues(state) {
        if (!playScene || state.phase !== 'CollectingAnswers' || !vibrateAllowed()) {
            return;
        }

        const yo = String(state.yourNickname ?? state.YourNickname ?? '').trim();
        const raw = state.waitingAnswerNicknames ?? state.WaitingAnswerNicknames;
        const wait = normalizeWaitingNicknames(raw);
        const youPending =
            yo.length > 0 &&
            wait.some((n) => String(n ?? '').trim().toLowerCase() === yo.toLowerCase()) &&
            !state.alreadySubmittedAnswerThisRound;

        if (youPending && !vibrateTurnNotifiedForRound) {
            vibrateTurnNotifiedForRound = true;
            triggerHaptic('info');
        }

        const endMs = parseRoundEndsMs(state);
        const waitingSubmit = !state.alreadySubmittedAnswerThisRound;
        const secLeft = waitingSubmit && endMs ? secondsRemainingToEnd(endMs) : null;

        if (waitingSubmit && secLeft != null && secLeft > 0 && secLeft <= 5) {
            if (secLeft !== vibrateLastCountdownSecond) {
                vibrateLastCountdownSecond = secLeft;
                triggerHaptic(secLeft <= 2 ? 'urgent' : 'warn');
            }
        }
    }

    function updateAnswerWaitingLine(state) {
        const el = document.getElementById('answerWaitingLine');
        if (!el) {
            return;
        }

        const raw = state.waitingAnswerNicknames ?? state.WaitingAnswerNicknames;
        const names = normalizeWaitingNicknames(raw);
        const phrase = formatWaitingAnswerPhrase(names);
        if (!phrase) {
            hideAnswerWaitingLine();
            return;
        }

        el.style.display = '';
        el.textContent = phrase;
    }

    function updateAnswerProgressIndicator(state) {
        const line = document.getElementById('answerProgressLine');
        const text = document.getElementById('answerProgressText');
        if (!line || !text) {
            return;
        }

        const done = Number(state.answersSubmittedCount ?? 0);
        const total = Number(state.answersRoomMemberTotal ?? 0);
        line.style.display = '';
        text.textContent = `${done}/${total}`;
    }

    function setAnswerComposerLocked(locked) {
        const textarea = document.getElementById('answerField');
        const sendBtn = document.getElementById('sendAnswerBtn');
        const notice = document.getElementById('answerSentNotice');
        const label = document.getElementById('answerComposerLabel');
        if (textarea) {
            textarea.disabled = locked;
            if (locked) {
                textarea.value = '';
                textarea.style.display = 'none';
            } else {
                textarea.style.display = '';
                textarea.placeholder = 'Cevabını yaz...';
            }
        }

        if (sendBtn) {
            sendBtn.disabled = locked;
            sendBtn.style.display = locked ? 'none' : '';
        }

        if (notice) {
            notice.style.display = locked ? '' : 'none';
        }

        if (label) {
            label.style.display = locked ? 'none' : '';
        }
    }

    function setQuestion(text) {
        const t = text || 'Burada güzel soru çıkması lazım!';
        const headline = document.getElementById('questionTitle');
        const revealHeadline = document.getElementById('revealQuestionTitle');
        if (headline) {
            headline.textContent = t;
        }

        if (revealHeadline) {
            revealHeadline.textContent = t;
        }
    }

    function setNickBadge(txt) {
        const badge = document.getElementById('youBadge');
        if (!badge || !txt) {
            return;
        }

        badge.textContent = txt;
    }

    function renderPresence(entries) {
        const hostWrap = document.getElementById('lobbyPresence');
        const badge = document.getElementById('lobbyPresenceCount');
        if (!hostWrap || !badge) {
            return;
        }

        badge.textContent = entries.length.toString();
        mountKickableGuests(hostWrap, entries);
    }

    function animateDeck(cards) {
        const holder = document.getElementById('deckGrid');
        if (!holder) {
            return;
        }

        holder.innerHTML = '';
        cards.forEach((textValue, idx) => {
            setTimeout(() => {
                const bubble = document.createElement('article');
                bubble.className =
                    'answer-card rounded-4 p-4 border border-opacity-50 border-secondary bg-dark bg-opacity-50 text-white';
                bubble.innerHTML =
                    `<p class="fw-semibold text-warning small mb-2">Kart ${idx + 1}</p><p class="fs-6 mb-0">${encodeText(textValue ?? '')}</p>`;
                holder.appendChild(bubble);
            }, idx * 135);
        });
    }

    function clipboardCopy(copyText) {
        if (!copyText) {
            toast('Kopyalayacak metin eksik 📋');
            return;
        }

        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(copyText).then(() => toast('Panoya kopyalandı ✅')).catch(console.error);
        } else {
            const helper = document.createElement('textarea');
            helper.value = copyText;
            document.body.appendChild(helper);
            helper.select();
            document.execCommand('copy');
            helper.remove();
            toast('Kopyalama denendi (tarayıcı kısıtı olabilir) 🫱');
        }
    }

    async function copyInviteInputs() {
        const inputEl = document.getElementById('inviteLink');
        if (!inputEl?.value) {
            return;
        }

        clipboardCopy(inputEl.value);
    }

    function toast(msg) {
        console.info('[Ifsa]', msg);
        const snack = document.getElementById('ifsSnack');
        if (snack) {
            snack.textContent = msg || '';
            snack.classList.remove('ifs-push-show');
            void snack.offsetWidth;
            snack.classList.add('ifs-push-show');
            window.clearTimeout(window.__IFS_SNACK_HIDE);
            window.__IFS_SNACK_HIDE = window.setTimeout(() => {
                snack.classList.remove('ifs-push-show');
            }, 3600);
            return;
        }

        const sink = document.getElementById('feedbackText');
        if (sink) {
            sink.style.display = 'block';
            sink.textContent = msg || '';
            window.clearTimeout(window.__IFS_TOAST_TIMER);
            window.__IFS_TOAST_TIMER = window.setTimeout(() => {
                sink.style.display = 'none';
            }, 3200);
            return;
        }

        alert(msg);
    }

    function hideFeedback() {
        const fb = document.getElementById('feedbackText');
        if (fb) {
            fb.style.display = 'none';
        }
    }

    function bindClick(id, handler) {
        const el = byId(id);
        if (el) {
            el.addEventListener('click', handler);
        }
    }

    function bindChange(id, handler) {
        const el = byId(id);
        if (el) {
            el.addEventListener('change', handler);
        }
    }

    function toggleSection(id, show) {
        const el = byId(id);
        if (!el) {
            return;
        }

        el.style.display = show ? '' : 'none';
    }

    function normalizeNickname(txt) {
        return (txt || '').trim().slice(0, 32);
    }

    function hyphenate(raw) {
        if (!raw) {
            return raw;
        }

        const str = raw.toString();
        if (str.length !== 32) {
            return str;
        }

        return `${str.slice(0, 8)}-${str.slice(8, 12)}-${str.slice(12, 16)}-${str.slice(16, 20)}-${str.slice(20)}`;
    }

    function getInputValue(id) {
        return byId(id)?.value ?? '';
    }

    function setSelectValueSafe(id, value) {
        const select = byId(id);
        if (!select || value === undefined || value === null) {
            return;
        }

        const matcher = [...select.options].find((opt) => opt.value === String(value));
        if (matcher) {
            select.value = matcher.value;
        }
    }

    function byId(id) {
        return document.getElementById(id);
    }

    function buildQr(target, uri) {
        if (!target || !uri) {
            return;
        }

        const w = Number(target.width || target.clientWidth || 240);
        const h = Number(target.height || target.clientHeight || 240);

        if (target.tagName === 'IMG') {
            target.src = `https://api.qrserver.com/v1/create-qr-code/?size=${w}x${h}&data=${encodeURIComponent(uri)}`;
            target.alt = 'Davet QR kodu';
            return;
        }

        if (typeof QRCode === 'undefined') {
            return;
        }

        QRCode.toCanvas(target, uri ?? '', {
            scale: 4,
            margin: 1,
        }, (error) => {
            if (error) {
                console.error(error);
            }
        });
    }

    function encodeText(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
})();
