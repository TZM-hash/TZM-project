(function () {
    'use strict';
    const root = document.querySelector('[data-offline-equipment]');
    if (!root || !('indexedDB' in window)) return;
    const DB_NAME = 'engineering-manager-offline';
    const DB_VERSION = 2;
    const MAX_PHOTOS = 20;
    const MAX_PHOTO_BYTES = 3 * 1024 * 1024;
    const userId = root.dataset.userId;
    const form = root.querySelector('[data-equipment-draft-form]');
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const message = root.querySelector('[data-equipment-offline-message]');
    let dbPromise;

    function openDatabase() {
        if (dbPromise) return dbPromise;
        dbPromise = new Promise(function (resolve, reject) {
            const request = indexedDB.open(DB_NAME, DB_VERSION);
            request.onupgradeneeded = function () {
                const database = request.result;
                if (!database.objectStoreNames.contains('drafts')) database.createObjectStore('drafts', { keyPath: ['userId', 'clientDraftId'] });
                if (!database.objectStoreNames.contains('photos')) database.createObjectStore('photos', { keyPath: ['userId', 'clientAttachmentId'] });
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

    async function requestStore(name, mode, operation) {
        const database = await openDatabase();
        return new Promise(function (resolve, reject) {
            const request = operation(database.transaction(name, mode).objectStore(name));
            request.onsuccess = function () { resolve(request.result); };
            request.onerror = function () { reject(request.error); };
        });
    }
    const put = (name, value) => requestStore(name, 'readwrite', store => store.put(value));
    const getAll = name => requestStore(name, 'readonly', store => store.getAll());

    function readPeriods() {
        return Array.from(root.querySelectorAll('[data-equipment-period]')).map(row => ({
            startDate: row.querySelector('[name="periodStart"]').value,
            endDate: row.querySelector('[name="periodEnd"]').value,
            periodType: Number(row.querySelector('[name="periodType"]').value),
            isChargeable: row.querySelector('[name="periodChargeable"]').checked,
            notes: row.querySelector('[name="periodNotes"]').value || null
        })).filter(item => item.startDate && item.endDate);
    }

    async function saveDraft(event) {
        event.preventDefault();
        const data = new FormData(form);
        const clientDraftId = data.get('clientDraftId') || crypto.randomUUID();
        form.elements.clientDraftId.value = clientDraftId;
        const draft = {
            userId, clientDraftId, operationId: crypto.randomUUID(),
            equipmentId: data.get('equipmentId'), projectId: data.get('projectId'), legalEntityId: data.get('legalEntityId'),
            entryDate: data.get('entryDate'), exitDate: data.get('exitDate') || null,
            notes: data.get('notes') || null, periods: readPeriods(), updatedAt: new Date().toISOString()
        };
        await put('equipmentDrafts', draft);
        await put('equipmentQueue', { id: `${userId}:${clientDraftId}`, userId, clientDraftId, operationId: draft.operationId, nextAttemptAt: Date.now(), attempt: 0 });
        message.textContent = navigator.onLine ? '已保存到本机，正在等待同步。' : '当前离线，草稿已安全保存在本机。';
    }

    async function savePhotos(files) {
        const existing = (await getAll('equipmentPhotos')).filter(item => item.userId === userId);
        if (existing.length + files.length > MAX_PHOTOS) throw new Error('每条记录最多 20 张照片。');
        for (const file of files) {
            if (file.size > MAX_PHOTO_BYTES) throw new Error('单张照片压缩后不能超过 3 MB。');
            await put('equipmentPhotos', { userId, clientAttachmentId: crypto.randomUUID(), clientDraftId: form.elements.clientDraftId.value, name: file.name, type: file.type, size: file.size, blob: file });
        }
    }

    async function clearUserData() {
        const database = await openDatabase();
        for (const name of ['equipmentDrafts', 'equipmentPhotos', 'equipmentQueue']) {
            const values = await getAll(name);
            const transaction = database.transaction(name, 'readwrite');
            const store = transaction.objectStore(name);
            values.filter(item => item.userId === userId).forEach(item => store.delete(name === 'equipmentQueue' ? item.id : [userId, name === 'equipmentDrafts' ? item.clientDraftId : item.clientAttachmentId]));
        }
        message.textContent = '当前用户的设备离线数据已清除。';
    }

    async function processQueue() {
        if (!navigator.onLine) return;
        const queue = (await getAll('equipmentQueue')).filter(item => item.userId === userId && item.nextAttemptAt <= Date.now());
        const drafts = await getAll('equipmentDrafts');
        for (const item of queue) {
            const draft = drafts.find(value => value.userId === userId && value.clientDraftId === item.clientDraftId);
            if (!draft) continue;
            const payload = {
                clientDraftId: draft.clientDraftId, operationId: item.operationId, baseServerVersion: draft.serverVersion || null,
                usage: { id: draft.serverUsageId || null, equipmentId: draft.equipmentId, projectId: draft.projectId, legalEntityId: draft.legalEntityId, leaseAgreementId: null, entryDate: draft.entryDate, exitDate: draft.exitDate, rentMode: 1, monthlyProrationMode: 2, unitRate: 0, sharedUsageOverride: false, sharedUsageReason: null, periods: draft.periods, concurrencyStamp: draft.serverVersion || null, reason: '设备离线同步' }
            };
            try {
                const response = await fetch('/Equipment/Offline?handler=Sync', { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(payload) });
                const result = await response.json();
                if (!response.ok || result.isConflict) throw new Error(result.error || result.message || '同步失败');
                draft.serverUsageId = result.usageId; draft.serverVersion = result.serverVersion; await put('equipmentDrafts', draft);
                await requestStore('equipmentQueue', 'readwrite', store => store.delete(item.id));
                await processPhotos(draft.clientDraftId);
                message.textContent = '设备现场草稿已同步。';
            } catch (error) {
                item.attempt += 1; item.nextAttemptAt = Date.now() + Math.min(300000, Math.pow(2, item.attempt) * 1000); await put('equipmentQueue', item);
                message.textContent = error.message;
            }
        }
    }

    async function processPhotos(clientDraftId) {
        const photos = (await getAll('equipmentPhotos')).filter(item => item.userId === userId && item.clientDraftId === clientDraftId);
        for (const photo of photos) {
            const body = new FormData(); body.append('photo', photo.blob, photo.name); body.append('description', '设备现场照片');
            const response = await fetch(`/Equipment/Offline?handler=Photo&clientDraftId=${encodeURIComponent(clientDraftId)}&clientAttachmentId=${encodeURIComponent(photo.clientAttachmentId)}`, { method: 'POST', headers: { 'RequestVerificationToken': token }, body });
            if (!response.ok) throw new Error((await response.json()).error || '照片同步失败');
            await requestStore('equipmentPhotos', 'readwrite', store => store.delete([userId, photo.clientAttachmentId]));
        }
    }

    form.addEventListener('submit', saveDraft);
    root.querySelector('[data-equipment-photos]').addEventListener('change', event => savePhotos(Array.from(event.target.files || [])).catch(error => { message.textContent = error.message; }));
    root.querySelector('[data-equipment-clear]').addEventListener('click', clearUserData);
    window.addEventListener('online', function () { message.textContent = '网络已恢复，正在同步设备草稿。'; processQueue(); });
    openDatabase().then(processQueue);
}());
