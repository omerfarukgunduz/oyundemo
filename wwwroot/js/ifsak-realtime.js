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

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(cfg.hubPath)
        .withAutomaticReconnect([0, 1500, 4000])
        .build();

    connection.on('lobbyUpdated', (people) => renderPresence(Array.isArray(people) ? people : []));
    connection.on('stateFull', handleStateEnvelope);
    connection.on('chatMessage', appendChatMessage);
    connection.on('kickedFromRoom', async () => {
        toast('Oda kurucusu seni odadan cikardi.');
        try {
            await connection.stop();
        } catch (_) {
            // ignore
        }

        window.location.assign(`/Room/Join?code=${encodeURIComponent(cfg.roomCode ?? '')}`);
    });

    async function invokeHub(method, ...args) {
        try {
            const okPacket = await connection.invoke(method, ...args);
            if (!okPacket?.ok) {
                toast(okPacket?.error || 'Bu adim simdilik olmadı.');
            }

            return okPacket;
        } catch (error) {
            console.error(method, error);
            toast(`Baglantida kirik var: ${error?.message ?? 'bilinmeyen 🫠'}`);
            return null;
        }
    }

    document.addEventListener('DOMContentLoaded', () => {
        if (!window.signalR) {
            toast('SignalR kutuphanesi eksik 📡');
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
        bindClick('hostNextRoundBtn', () => invokeHub('HostNextQuestion', cfg.roomCode, hyphenate(cfg.identity)));
        wireRoomChat();
        setNickBadge(cfg.nickname);
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

    function handleStateEnvelope(state) {
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

    /** Host “At” tuslari (delegasyon, tek kayit). */
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

            if (!window.confirm('Bu oyuncuyu odadan cikarmak istiyor musun?')) {
                return;
            }

            evt.preventDefault();
            await invokeHub('HostKickMember', cfg.roomCode, hyphenate(cfg.identity), hyphenate(rawId));
        });
    }

    function hydratePlayScreens(state) {
        setNickBadge(state.yourNickname);
        toggleSection('collectPanel', state.phase === 'CollectingAnswers');
        toggleSection('revealPanel', state.phase === 'Revealed');
        toggleSection('hostRevealPanel', !!(state.phase === 'CollectingAnswers' && state.youAreHost));
        toggleSection('hostNextPanel', !!(state.phase === 'Revealed' && state.youAreHost));

        if (state.phase === 'CollectingAnswers') {
            const rid = state.currentRoundId ?? null;
            if (rid != null && rid !== lastAnswerSyncedRoundId) {
                const answerEl = document.getElementById('answerField');
                if (answerEl) {
                    answerEl.value = '';
                }

                lastAnswerSyncedRoundId = rid;
            }

            setQuestion(state.questionText);
            syncAnswerComposerLockedFromState(state);
            updateAnswerProgressIndicator(state);
        } else {
            setAnswerComposerLocked(false);
            hideAnswerProgressIndicator();
        }

        if (state.phase === 'Revealed' && Array.isArray(state.shuffledCards)) {
            animateDeck(state.shuffledCards);
        }
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

    /** Lobide liste + sayac; oyunda sadece `updatePlayHostKickPanel` kullanir. */
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
            ? '☆ HOST'
            : guest.isConnected
                ? '🟢 canlı masada'
                : '💤 ara verdi';

        let kickMarkup = `<span class="small">${vibe}</span>`;
        if (cfg.isHost && guest.isHost === false) {
            const midRaw = encodeAttr(guestMemberGuidString(guest));
            kickMarkup +=
                `<button type="button" class="btn btn-sm btn-outline-danger rounded-pill" data-action="host-kick-member" data-member-id="${midRaw}">Masadan cikar</button>`;
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
        if (textarea) {
            textarea.disabled = locked;
            if (locked && !textarea.value.trim()) {
                textarea.placeholder = 'Cevabın gönderildi';
            }

            if (!locked) {
                textarea.placeholder = 'En dramatik tepkiyi seç ve yaz 😉';
            }
        }

        if (sendBtn) {
            sendBtn.disabled = locked;
        }

        if (notice) {
            notice.style.display = locked ? '' : 'none';
        }
    }

    function setQuestion(text) {
        const headline = document.getElementById('questionTitle');
        if (headline) {
            headline.textContent = text || 'Burada güzel soru çıkması lazım!';
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
                    `<p class="fw-semibold text-warning small mb-2">KART ${idx + 1}</p><p class="fs-6 mb-0">${encodeText(textValue ?? '')}</p>`;
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
            navigator.clipboard.writeText(copyText).then(() => toast('Link arkadasina ✅')).catch(console.error);
        } else {
            const helper = document.createElement('textarea');
            helper.value = copyText;
            document.body.appendChild(helper);
            helper.select();
            document.execCommand('copy');
            helper.remove();
            toast('Kopyalamayi denedin 🫱');
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
