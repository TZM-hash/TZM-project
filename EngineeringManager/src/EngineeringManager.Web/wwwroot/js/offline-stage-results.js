(function () {
    'use strict';

    const root = document.querySelector('[data-offline-stage-results]');
    if (!root || !('indexedDB' in window)) return;

    const DB_NAME = 'engineering-manager-offline';
    const DB_VERSION = 2;
    const MAX_PHOTOS = 20;
    const MAX_EDGE = 1920;
    const MAX_PHOTO_BYTES = 3 * 1024 * 1024;
    const userId = root.dataset.userId;
    const form = root.querySelector('[data-offline-draft-form]');
    const projectSelect = root.querySelector('[data-offline-project]');
    const contractSelect = root.querySelector('[data-offline-contract]');
    const linesHost = root.querySelector('[data-offline-lines]');
    const photoInput = root.querySelector('[data-offline-photos]');
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const message = root.querySelector('[data-offline-message]');
    const connectionBadge = document.querySelector('[data-offline-connection]');
    const conflictPanel = root.querySelector('[data-offline-conflict-panel]');
    const optionsNode = document.getElementById('offline-project-options');
    const initialProjects = optionsNode ? JSON.parse(optionsNode.textContent || '[]') : [];
    let projects = normalizeProjects(initialProjects);
    let dbPromise;

    function normalizeProjects(items) {
        return items.map(function (project) {
            return {
                id: project.id || project.Id,
                number: project.number || project.Number,
                name: project.name || project.Name,
                contracts: (project.contracts || project.Contracts || []).map(function (contract) {
                    return {
                        id: contract.id || contract.Id,
                        number: contract.number || contract.Number,
                        name: contract.name || contract.Name,
                        lineItems: (contract.lineItems || contract.LineItems || []).map(function (line) {
                            return { id: line.id || line.Id, code: line.code || line.Code, name: line.name || line.Name, unit: line.unit || line.Unit };
                        })
                    };
                })
            };
        });
    }

    function openDatabase() {
        if (dbPromise) return dbPromise;
        dbPromise = new Promise(function (resolve, reject) {
            const request = indexedDB.open(DB_NAME, DB_VERSION);
            request.onupgradeneeded = function () {
                const database = request.result;
                if (!database.objectStoreNames.contains('drafts')) database.createObjectStore('drafts', { keyPath: ['userId', 'clientDraftId'] });
                if (!database.objectStoreNames.contains('photos')) {
                    const photos = database.createObjectStore('photos', { keyPath: ['userId', 'clientAttachmentId'] });
                    photos.createIndex('byDraft', ['userId', 'clientDraftId'], { unique: false });
                }
                if (!database.objectStoreNames.contains('queue')) database.createObjectStore('queue', { keyPath: 'id' });
                if (!database.objectStoreNames.contains('metadata')) database.createObjectStore('metadata', { keyPath: ['userId', 'key'] });
                if (!database.objectStoreNames.contains('equipmentDrafts')) database.createObjectStore('equipmentDrafts', { keyPath: ['userId', 'clientDraftId'] });
                if (!database.objectStoreNames.contains('equipmentPhotos')) database.createObjectStore('equipmentPhotos', { keyPath: ['userId', 'clientAttachmentId'] });
                if (!database.objectStoreNames.contains('equipmentQueue')) database.createObjectStore('equipmentQueue', { keyPath: 'id' });
            };
            request.onsuccess = function () { resolve(request.result); };
            request.onerror = function () { reject(request.error); };
        });
        return dbPromise;
    }

    async function storeRequest(storeName, mode, operation) {
        const database = await openDatabase();
        return new Promise(function (resolve, reject) {
            const transaction = database.transaction(storeName, mode);
            const request = operation(transaction.objectStore(storeName));
            request.onsuccess = function () { resolve(request.result); };
            request.onerror = function () { reject(request.error); };
        });
    }

    const put = function (store, value) { return storeRequest(store, 'readwrite', function (target) { return target.put(value); }); };
    const remove = function (store, key) { return storeRequest(store, 'readwrite', function (target) { return target.delete(key); }); };
    const getAll = function (store) { return storeRequest(store, 'readonly', function (target) { return target.getAll(); }); };

    function setMessage(text, isError) {
        message.textContent = text;
        message.classList.toggle('text-danger', Boolean(isError));
    }

    function updateConnection() {
        const online = navigator.onLine;
        connectionBadge.textContent = online ? '网络可用' : '当前离线';
        connectionBadge.classList.toggle('is-offline', !online);
    }

    function fillProjects(selectedId) {
        projectSelect.innerHTML = '<option value="">请选择项目</option>';
        projects.forEach(function (project) {
            const option = document.createElement('option');
            option.value = project.id;
            option.textContent = project.number + ' · ' + project.name;
            option.selected = project.id === selectedId;
            projectSelect.appendChild(option);
        });
        fillContracts();
    }

    function selectedProject() { return projects.find(function (item) { return item.id === projectSelect.value; }); }
    function selectedContract() { return selectedProject()?.contracts.find(function (item) { return item.id === contractSelect.value; }); }

    function fillContracts(selectedId, lineValues) {
        contractSelect.innerHTML = '<option value="">不关联合同</option>';
        (selectedProject()?.contracts || []).forEach(function (contract) {
            const option = document.createElement('option');
            option.value = contract.id;
            option.textContent = contract.number + ' · ' + contract.name;
            option.selected = contract.id === selectedId;
            contractSelect.appendChild(option);
        });
        renderLines(lineValues || []);
    }

    function renderLines(values) {
        const byId = new Map(values.map(function (line) { return [line.contractLineItemId, line]; }));
        const lines = selectedContract()?.lineItems || [];
        linesHost.innerHTML = lines.length ? '<p class="eyebrow">本期工程量</p><div class="offline-line-grid"></div>' : '<p class="panel-copy">选择合同后可填写工程量清单。</p>';
        const grid = linesHost.querySelector('.offline-line-grid');
        if (!grid) return;
        lines.forEach(function (line) {
            const previous = byId.get(line.id);
            const row = document.createElement('div');
            row.className = 'offline-line-row';
            row.dataset.lineId = line.id;
            row.innerHTML = '<span><strong></strong><small></small></span><label>本期量<input type="number" min="0" step="0.0001" data-line-quantity></label><label>备注<input maxlength="1000" data-line-notes></label>';
            row.querySelector('strong').textContent = line.code + ' · ' + line.name;
            row.querySelector('small').textContent = line.unit;
            row.querySelector('[data-line-quantity]').value = previous?.periodQuantity || '';
            row.querySelector('[data-line-notes]').value = previous?.notes || '';
            grid.appendChild(row);
        });
    }

    function readLines() {
        return Array.from(linesHost.querySelectorAll('[data-line-id]')).map(function (row) {
            const quantity = row.querySelector('[data-line-quantity]').value;
            return quantity === '' ? null : {
                contractLineItemId: row.dataset.lineId,
                periodQuantity: Number(quantity),
                notes: row.querySelector('[data-line-notes]').value || null
            };
        }).filter(Boolean);
    }

    function uuid() { return crypto.randomUUID(); }

    async function canvasBlob(canvas, quality) {
        return new Promise(function (resolve) { canvas.toBlob(resolve, 'image/jpeg', quality); });
    }

    async function loadImage(file) {
        if ('createImageBitmap' in window) return createImageBitmap(file);
        return new Promise(function (resolve, reject) {
            const image = new Image();
            const url = URL.createObjectURL(file);
            image.onload = function () { URL.revokeObjectURL(url); resolve(image); };
            image.onerror = function () { URL.revokeObjectURL(url); reject(new Error('照片无法读取。')); };
            image.src = url;
        });
    }

    async function compressPhoto(file) {
        const image = await loadImage(file);
        const ratio = Math.min(1, MAX_EDGE / Math.max(image.width, image.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(image.width * ratio));
        canvas.height = Math.max(1, Math.round(image.height * ratio));
        const context = canvas.getContext('2d', { alpha: false });
        context.drawImage(image, 0, 0, canvas.width, canvas.height);
        if (image.close) image.close();
        let quality = 0.86;
        let blob = await canvasBlob(canvas, quality);
        while (blob && blob.size > MAX_PHOTO_BYTES && quality > 0.4) {
            quality -= 0.1;
            blob = await canvasBlob(canvas, quality);
        }
        if (!blob || blob.size > MAX_PHOTO_BYTES) throw new Error('照片压缩后仍超过 3 MB，请改用较小照片。');
        return blob;
    }

    async function saveSelectedPhotos(clientDraftId) {
        const existing = (await getAll('photos')).filter(function (item) { return item.userId === userId && item.clientDraftId === clientDraftId; });
        const files = Array.from(photoInput.files || []);
        if (existing.length + files.length > MAX_PHOTOS) throw new Error('每份草稿最多保存 20 张照片。');
        for (const file of files) {
            const blob = await compressPhoto(file);
            await put('photos', {
                userId: userId,
                clientDraftId: clientDraftId,
                clientAttachmentId: uuid(),
                originalFileName: file.name.replace(/\.[^.]+$/, '') + '.jpg',
                contentType: 'image/jpeg',
                sizeBytes: blob.size,
                blob: blob,
                status: 'pending',
                error: null
            });
        }
        photoInput.value = '';
    }

    async function saveDraft(event) {
        event.preventDefault();
        const data = new FormData(form);
        const clientDraftId = data.get('clientDraftId') || uuid();
        const existing = (await getAll('drafts')).find(function (item) { return item.userId === userId && item.clientDraftId === clientDraftId; });
        const draft = {
            userId: userId,
            clientDraftId: clientDraftId,
            operationId: uuid(),
            serverStageResultId: existing?.serverStageResultId || null,
            baseServerVersion: existing?.baseServerVersion || null,
            projectId: data.get('projectId'),
            contractId: data.get('contractId') || null,
            title: data.get('title'),
            resultType: Number(data.get('resultType')),
            resultDate: data.get('resultDate'),
            description: data.get('description') || null,
            qualityResult: Number(data.get('qualityResult')),
            lines: readLines(),
            updatedAt: new Date().toISOString(),
            serverSnapshot: null
        };
        if (!draft.projectId || !draft.title || !draft.resultDate) throw new Error('项目、阶段名称和成果日期不能为空。');
        await saveSelectedPhotos(clientDraftId);
        await put('drafts', draft);
        await put('queue', { id: userId + ':' + clientDraftId, userId: userId, clientDraftId: clientDraftId, status: 'pending', attempts: 0, nextAttemptAt: 0 });
        form.querySelector('[data-client-draft-id]').value = clientDraftId;
        setMessage('草稿已保存到本机。', false);
        await updateCounts();
        if (navigator.onLine) await syncAll();
    }

    async function postJson(handler, payload) {
        return fetch('/StageResults/Offline?handler=' + handler, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(payload),
            credentials: 'same-origin'
        });
    }

    async function syncPhotos(draft) {
        const photos = (await getAll('photos')).filter(function (item) { return item.userId === userId && item.clientDraftId === draft.clientDraftId && item.status !== 'synced'; });
        for (const photo of photos) {
            const body = new FormData();
            body.append('clientDraftId', draft.clientDraftId);
            body.append('clientAttachmentId', photo.clientAttachmentId);
            body.append('photo', photo.blob, photo.originalFileName);
            body.append('category', '1');
            const response = await fetch('/StageResults/Offline?handler=Photo', { method: 'POST', headers: { 'RequestVerificationToken': token }, body: body, credentials: 'same-origin' });
            if (!response.ok) {
                photo.status = 'failed';
                photo.error = (await response.json().catch(function () { return {}; })).error || '照片同步失败。';
                await put('photos', photo);
                throw new Error(photo.error);
            }
            photo.status = 'synced';
            photo.error = null;
            await put('photos', photo);
        }
    }

    async function syncQueueItem(item) {
        const draft = (await getAll('drafts')).find(function (candidate) { return candidate.userId === userId && candidate.clientDraftId === item.clientDraftId; });
        if (!draft) { await remove('queue', item.id); return; }
        const response = await postJson('Sync', draft);
        if (!response.ok) throw new Error((await response.json().catch(function () { return {}; })).error || '草稿同步失败。');
        const result = await response.json();
        if (result.status === 3) {
            draft.serverSnapshot = result.serverSnapshot;
            await put('drafts', draft);
            item.status = 'conflict';
            await put('queue', item);
            renderConflict(draft);
            return;
        }
        if (result.status !== 1) throw new Error(result.errorMessage || '草稿同步失败。');
        draft.serverStageResultId = result.serverStageResultId;
        draft.baseServerVersion = result.serverVersion;
        draft.serverSnapshot = null;
        await put('drafts', draft);
        await syncPhotos(draft);
        await remove('queue', item.id);
    }

    async function syncAll() {
        if (!navigator.onLine) { setMessage('当前离线，草稿会继续保存在本机。', false); return; }
        const now = Date.now();
        const items = (await getAll('queue')).filter(function (item) { return item.userId === userId && item.status !== 'conflict' && item.nextAttemptAt <= now; });
        for (const item of items) {
            try {
                await syncQueueItem(item);
                setMessage('离线草稿已同步。', false);
            } catch (error) {
                item.attempts += 1;
                item.status = 'failed';
                item.error = error.message;
                item.nextAttemptAt = Date.now() + Math.min(300000, 1000 * Math.pow(2, item.attempts));
                await put('queue', item);
                await postJson('Failure', { clientDraftId: item.clientDraftId, errorMessage: error.message }).catch(function () { });
                setMessage(error.message, true);
            }
        }
        await updateCounts();
    }

    function renderConflict(draft) {
        conflictPanel.hidden = false;
        conflictPanel.innerHTML = '<h3>发现版本冲突</h3><p>服务器版本：<strong></strong></p><div class="offline-actions"><button type="button" class="button button--secondary" data-conflict-keep-server>保留服务器版本</button><button type="button" class="button button--secondary" data-conflict-retry-local>基于服务器版本重试本机内容</button></div>';
        conflictPanel.querySelector('strong').textContent = draft.serverSnapshot?.title || '服务器草稿';
        conflictPanel.querySelector('[data-conflict-keep-server]').addEventListener('click', async function () {
            const server = draft.serverSnapshot;
            if (!server) return;
            Object.assign(draft, {
                serverStageResultId: server.stageResultId,
                baseServerVersion: server.serverVersion,
                projectId: server.projectId,
                contractId: server.contractId,
                title: server.title,
                resultType: server.resultType,
                resultDate: server.resultDate,
                description: server.description,
                qualityResult: server.qualityResult,
                lines: server.lines,
                serverSnapshot: null
            });
            await put('drafts', draft);
            await remove('queue', userId + ':' + draft.clientDraftId);
            conflictPanel.hidden = true;
            loadDraft(draft);
            await updateCounts();
        });
        conflictPanel.querySelector('[data-conflict-retry-local]').addEventListener('click', async function () {
            draft.baseServerVersion = draft.serverSnapshot.serverVersion;
            draft.operationId = uuid();
            draft.serverSnapshot = null;
            await put('drafts', draft);
            await put('queue', { id: userId + ':' + draft.clientDraftId, userId: userId, clientDraftId: draft.clientDraftId, status: 'pending', attempts: 0, nextAttemptAt: 0 });
            conflictPanel.hidden = true;
            await syncAll();
        });
    }

    function loadDraft(draft) {
        form.querySelector('[data-client-draft-id]').value = draft.clientDraftId;
        fillProjects(draft.projectId);
        fillContracts(draft.contractId, draft.lines);
        form.elements.title.value = draft.title || '';
        form.elements.resultDate.value = draft.resultDate || '';
        form.elements.resultType.value = String(draft.resultType || 1);
        form.elements.qualityResult.value = String(draft.qualityResult || 1);
        form.elements.description.value = draft.description || '';
        if (draft.serverSnapshot) renderConflict(draft);
    }

    async function updateCounts() {
        const queue = (await getAll('queue')).filter(function (item) { return item.userId === userId; });
        const photos = (await getAll('photos')).filter(function (item) { return item.userId === userId; });
        root.querySelector('[data-offline-pending]').textContent = queue.filter(function (item) { return item.status === 'pending'; }).length + photos.filter(function (item) { return item.status === 'pending'; }).length;
        root.querySelector('[data-offline-failed]').textContent = queue.filter(function (item) { return item.status === 'failed'; }).length + photos.filter(function (item) { return item.status === 'failed'; }).length;
        root.querySelector('[data-offline-conflicts]').textContent = queue.filter(function (item) { return item.status === 'conflict'; }).length;
        localStorage.setItem('engineering-manager-offline-counts:' + userId, JSON.stringify({
            pending: queue.filter(function (item) { return item.status === 'pending'; }).length + photos.filter(function (item) { return item.status === 'pending'; }).length,
            failed: queue.filter(function (item) { return item.status === 'failed'; }).length + photos.filter(function (item) { return item.status === 'failed'; }).length,
            conflicts: queue.filter(function (item) { return item.status === 'conflict'; }).length
        }));
    }

    async function clearUserData() {
        for (const store of ['drafts', 'photos', 'queue', 'metadata']) {
            const items = await getAll(store);
            for (const item of items.filter(function (candidate) { return candidate.userId === userId; })) {
                const key = store === 'queue' ? item.id : store === 'metadata' ? [item.userId, item.key] : store === 'drafts' ? [item.userId, item.clientDraftId] : [item.userId, item.clientAttachmentId];
                await remove(store, key);
            }
        }
        form.reset();
        form.querySelector('[data-client-draft-id]').value = '';
        conflictPanel.hidden = true;
        fillProjects();
        setMessage('本设备上的离线数据已清除。', false);
        await updateCounts();
    }

    async function checkStorage() {
        if (!navigator.storage?.estimate) return;
        const estimate = await navigator.storage.estimate();
        if (estimate.quota && estimate.usage / estimate.quota > 0.8) setMessage('浏览器存储空间已使用超过 80%，请尽快同步并清理旧草稿。', true);
    }

    async function initialize() {
        await openDatabase();
        await put('metadata', { userId: userId, key: 'projectOptions', value: projects, updatedAt: new Date().toISOString() });
        fillProjects();
        form.elements.resultDate.value = new Date().toISOString().slice(0, 10);
        const drafts = (await getAll('drafts')).filter(function (item) { return item.userId === userId; }).sort(function (a, b) { return b.updatedAt.localeCompare(a.updatedAt); });
        if (drafts[0]) loadDraft(drafts[0]);
        await updateCounts();
        await checkStorage();
        updateConnection();
        if (navigator.onLine) await syncAll();
    }

    projectSelect.addEventListener('change', function () { fillContracts(); });
    contractSelect.addEventListener('change', function () { renderLines([]); });
    form.addEventListener('submit', function (event) { saveDraft(event).catch(function (error) { setMessage(error.message, true); }); });
    root.querySelector('[data-offline-sync-now]').addEventListener('click', function () { syncAll(); });
    root.querySelector('[data-offline-clear]').addEventListener('click', function () { if (window.confirm('确定清除此设备上的全部离线草稿和照片吗？')) clearUserData(); });
    window.addEventListener('online', function () { updateConnection(); syncAll(); });
    window.addEventListener('offline', updateConnection);
    initialize().catch(function (error) { setMessage('离线存储初始化失败：' + error.message, true); });
}());
