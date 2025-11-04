<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { sha256 } from '@noble/hashes/sha256'
import { bytesToHex } from '@noble/hashes/utils'

const router = useRouter()

type UploadStatus = 'hashing' | 'done' | 'error'

interface UploadedFile {
  id: string
  name: string
  size: number
  status: UploadStatus
  hash?: string
  error?: string
}

interface VndbGame {
  title: string
  description: string | null
  developers: string[]
  released: string | null
  imageUrl: string | null
}

const defaultBackendUrl = 'https://dinzhen.api.potatovn.net'
const storageKey = 'galfingerprint.backendUrl'

const backendUrl = ref(defaultBackendUrl)
const backendInput = ref(defaultBackendUrl)
const showSettings = ref(false)
const settingsError = ref<string | null>(null)

const uploadedFiles = ref<UploadedFile[]>([])
const isDragActive = ref(false)
const recognizing = ref(false)
const recognizeError = ref<string | null>(null)

const vndbId = ref<string | null>(null)
const vndbLoading = ref(false)
const vndbError = ref<string | null>(null)
const vndbData = ref<VndbGame | null>(null)

if (typeof window !== 'undefined') {
  const saved = window.localStorage.getItem(storageKey)
  if (saved) {
    backendUrl.value = saved
    backendInput.value = saved
  }
}

const readyHashes = computed(() => {
  const unique = new Set<string>()
  for (const file of uploadedFiles.value) {
    if (file.status === 'done' && file.hash) {
      unique.add(file.hash)
    }
  }
  return Array.from(unique)
})

const canRecognize = computed(
  () => readyHashes.value.length > 0 && !uploadedFiles.value.some((file) => file.status === 'hashing') && !recognizing.value,
)

function openSettings() {
  backendInput.value = backendUrl.value
  settingsError.value = null
  showSettings.value = true
}

function closeSettings() {
  showSettings.value = false
}

function saveBackend() {
  const trimmed = backendInput.value.trim()
  if (!trimmed) {
    settingsError.value = '后端地址不能为空'
    return
  }

  try {
    new URL(trimmed)
  } catch {
    settingsError.value = '请输入合法的 URL（需包含协议）'
    return
  }

  backendUrl.value = trimmed
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(storageKey, trimmed)
  }
  settingsError.value = null
  showSettings.value = false
}

function resetBackend() {
  backendInput.value = defaultBackendUrl
  backendUrl.value = defaultBackendUrl
  if (typeof window !== 'undefined') {
    window.localStorage.removeItem(storageKey)
  }
  settingsError.value = null
  showSettings.value = false
}

function onDragEnter() {
  isDragActive.value = true
}

function onDragLeave(event: DragEvent) {
  if (!(event.relatedTarget instanceof Node) || !event.currentTarget) {
    isDragActive.value = false
    return
  }

  if (!(event.currentTarget as HTMLElement).contains(event.relatedTarget as Node)) {
    isDragActive.value = false
  }
}

function onDrop(event: DragEvent) {
  event.preventDefault()
  isDragActive.value = false

  const files = event.dataTransfer?.files
  if (!files?.length) {
    return
  }

  handleFiles(Array.from(files))
}

function onFileInputChange(event: Event) {
  const input = event.target as HTMLInputElement
  if (input.files?.length) {
    handleFiles(Array.from(input.files))
  }
  input.value = ''
}

function handleFiles(files: File[]) {
  for (const file of files) {
    addFile(file)
  }
}

function addFile(file: File) {
  const id = typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`
  const entry: UploadedFile = {
    id,
    name: file.name,
    size: file.size,
    status: 'hashing',
  }

  uploadedFiles.value = [...uploadedFiles.value, entry]

  if (!file.name.toLowerCase().endsWith('.exe')) {
    entry.status = 'error'
    entry.error = '仅支持 .exe 文件'
    updateEntry(entry)
    return
  }

  computeSha256(file)
    .then((hash) => {
      entry.hash = hash
      entry.status = 'done'
      updateEntry(entry)
    })
    .catch((error) => {
      console.error('hash computation failed', error)
      entry.status = 'error'
      entry.error = '计算哈希失败'
      updateEntry(entry)
    })
}

function updateEntry(updated: UploadedFile) {
  uploadedFiles.value = uploadedFiles.value.map((item) => (item.id === updated.id ? { ...updated } : item))
}

function removeFile(id: string) {
  uploadedFiles.value = uploadedFiles.value.filter((item) => item.id !== id)
}

function clearFiles() {
  uploadedFiles.value = []
  recognizeError.value = null
}

async function computeSha256(file: File) {
  const buffer = await file.arrayBuffer()
  const subtle = typeof crypto !== 'undefined' ? crypto.subtle : undefined

  if (subtle?.digest) {
    try {
      const hashBuffer = await subtle.digest('SHA-256', buffer)
      return bytesToHex(new Uint8Array(hashBuffer))
    } catch (error) {
      console.warn('crypto.subtle.digest failed, falling back to JS SHA-256', error)
    }
  }

  const hashBytes = sha256(new Uint8Array(buffer))
  return bytesToHex(hashBytes)
}

function buildEndpoint(path: string) {
  const base = backendUrl.value.trim().replace(/\/+$/, '') || defaultBackendUrl
  const suffix = path.startsWith('/') ? path : `/${path}`
  return `${base}${suffix}`
}

async function recognize() {
  if (!canRecognize.value) {
    return
  }

  recognizeError.value = null
  vndbError.value = null
  vndbData.value = null
  vndbId.value = null

  recognizing.value = true

  try {
    const response = await fetch(buildEndpoint('/game/byhash'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ hashes: readyHashes.value }),
    })

    if (response.status === 404) {
      recognizeError.value = '后端未找到匹配的游戏'
      recognizing.value = false
      return
    }

    if (!response.ok) {
      const message = await safeReadText(response)
      recognizeError.value = `后端请求失败：${response.status} ${message}`
      recognizing.value = false
      return
    }

    const payload = (await response.json()) as { vndbId?: string }
    if (!payload?.vndbId) {
      recognizeError.value = '后端返回数据缺少 vndbId'
      recognizing.value = false
      return
    }

    vndbId.value = payload.vndbId
  } catch (error) {
    recognizeError.value = `请求异常：${(error as Error).message}`
    recognizing.value = false
    return
  }

  recognizing.value = false
  if (vndbId.value) {
    await fetchVndbInfo(vndbId.value)
  }
}

async function safeReadText(response: Response) {
  try {
    return (await response.text()) || response.statusText
  } catch {
    return response.statusText
  }
}

async function fetchVndbInfo(id: string) {
  vndbLoading.value = true
  vndbError.value = null
  vndbData.value = null

  try {
    const response = await fetch('https://api.vndb.org/kana/vn', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        filters: ['id', '=', id],
        fields: 'title,description,image.url,released,developers.name',
      }),
    })

    if (!response.ok) {
      const message = await safeReadText(response)
      vndbError.value = `VNDB 请求失败：${response.status} ${message}`
      return
    }

    const payload = (await response.json()) as {
      results?: Array<{
        title?: string
        description?: string | null
        released?: string | null
        image?: { url?: string | null } | null
        developers?: Array<{ name?: string | null }> | null
      }>
    }

    const result = payload.results?.[0]
    if (!result) {
      vndbError.value = 'VNDB 未返回结果'
      return
    }

    vndbData.value = {
      title: result.title ?? id,
      description: sanitizeDescription(result.description ?? null),
      developers: (result.developers ?? []).map((dev) => dev?.name).filter((name): name is string => Boolean(name)),
      released: result.released ?? null,
      imageUrl: result.image?.url ?? null,
    }
  } catch (error) {
    vndbError.value = `VNDB 请求异常：${(error as Error).message}`
  } finally {
    vndbLoading.value = false
  }
}

function sanitizeDescription(description: string | null): string | null {
  if (!description) {
    return null
  }

  return description
    .replace(/\[(\/?)(b|i|u|s|quote|spoiler|raw|code|url=[^\]]*|url|center|size=[^\]]*|color=[^\]]*|list|\*|hr)\]/gi, '')
    .replace(/\[[^\]]+\]/g, '')
    .replace(/\r?\n/g, '\n')
    .trim()
}

function formatSize(bytes: number) {
  if (bytes < 1024) {
    return `${bytes} B`
  }
  const units = ['KB', 'MB', 'GB', 'TB']
  let size = bytes
  let unitIndex = 0
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex += 1
  }
  return `${size.toFixed(1)} ${units[unitIndex]}`
}

function goToGallery() {
  router.push('/gallery')
}
</script>

<template>
  <div class="layout">
    <header class="header">
      <div>
        <h1>Gal FingerPrint</h1>
        <p class="tagline">为视觉小说管理工具提供快速精准的哈希识别</p>
      </div>
      <div class="endpoint" title="当前使用的后端地址">
        <span>后端：</span>
        <a :href="backendUrl" target="_blank" rel="noopener">{{ backendUrl }}</a>
      </div>
    </header>

    <main class="panels">
      <section class="panel upload-panel">
        <h2>上传待识别的执行文件</h2>
        <p class="panel-hint">拖拽 .exe 文件进入下方区域，将自动计算 SHA-256 哈希值</p>

        <div
          class="drop-zone"
          :class="{ 'drop-active': isDragActive }"
          @dragenter.prevent="onDragEnter"
          @dragover.prevent
          @dragleave.prevent="onDragLeave"
          @drop="onDrop"
        >
          <p>拖放 .exe 文件到此区域</p>
          <p class="or">或</p>
          <label class="file-picker">
            选择文件
            <input type="file" accept=".exe" multiple @change="onFileInputChange" />
          </label>
          <p class="tip">哈希仅在本地浏览器计算，不会上传原始文件</p>
        </div>

        <ul v-if="uploadedFiles.length" class="file-list">
          <li v-for="file in uploadedFiles" :key="file.id" class="file-item">
            <div class="file-meta">
              <strong class="file-name">{{ file.name }}</strong>
              <span class="file-size">{{ formatSize(file.size) }}</span>
            </div>
            <div class="file-status">
              <span v-if="file.status === 'hashing'">正在计算 SHA-256...</span>
              <span v-else-if="file.status === 'done'" class="hash">
                <code>{{ file.hash }}</code>
              </span>
              <span v-else class="error">{{ file.error }}</span>
              <button type="button" class="icon-button" :disabled="recognizing" @click="removeFile(file.id)">✕</button>
            </div>
          </li>
        </ul>
        <p v-else class="panel-placeholder">尚未添加任何文件</p>

        <div class="panel-actions">
          <button class="primary" type="button" :disabled="!canRecognize" @click="recognize">
            <span v-if="recognizing">识别中...</span>
            <span v-else>识别</span>
          </button>
          <button
            class="secondary"
            type="button"
            :disabled="!uploadedFiles.length || recognizing"
            @click="clearFiles"
          >
            清空
          </button>
        </div>
        <p v-if="recognizeError" class="error feedback">{{ recognizeError }}</p>
      </section>

      <section class="panel result-panel">
        <h2>识别结果</h2>
        <div class="result-body">
          <template v-if="vndbId">
            <p class="vndb-id">
              识别到 VNDB ID：<a :href="`https://vndb.org/${vndbId}`" target="_blank" rel="noopener">{{ vndbId }}</a>
            </p>
            <div v-if="vndbLoading" class="loading">正在从 VNDB 获取详细信息...</div>
            <p v-else-if="vndbError" class="error feedback">{{ vndbError }}</p>
            <article v-else-if="vndbData" class="game-card">
              <img v-if="vndbData.imageUrl" :src="vndbData.imageUrl" alt="游戏封面" class="game-cover" />
              <div class="game-info">
                <h3 class="game-title">{{ vndbData.title }}</h3>
                <p v-if="vndbData.released" class="game-meta">发售日期：{{ vndbData.released }}</p>
                <p v-if="vndbData.developers.length" class="game-meta">开发商：{{ vndbData.developers.join('、') }}</p>
                <p v-if="vndbData.description" class="game-description">{{ vndbData.description }}</p>
              </div>
            </article>
            <p v-else class="placeholder">暂无更多信息</p>
          </template>
          <template v-else>
            <p class="placeholder">等待识别结果。请上传文件并点击识别。</p>
          </template>
        </div>
      </section>
    </main>

    <button class="gallery-fab" type="button" @click="goToGallery" aria-label="打开已记录游戏">🖼</button>
    <button class="settings-fab" type="button" @click="openSettings" aria-label="打开设置">⚙</button>

    <div v-if="showSettings" class="dialog-backdrop" @click.self="closeSettings">
      <div class="dialog" role="dialog" aria-modal="true">
        <h3>设置</h3>
        <label class="dialog-field">
          <span>后端地址</span>
          <input v-model="backendInput" type="text" placeholder="请输入后端地址" />
        </label>
        <p v-if="settingsError" class="error feedback">{{ settingsError }}</p>
        <div class="dialog-actions">
          <button type="button" class="secondary" @click="resetBackend">恢复默认</button>
          <div class="flex-spacer"></div>
          <button type="button" class="secondary" @click="closeSettings">取消</button>
          <button type="button" class="primary" @click="saveBackend">保存</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.layout {
  min-height: 100vh;
  padding: 32px clamp(16px, 4vw, 48px);
  display: flex;
  flex-direction: column;
  gap: 24px;
  background: linear-gradient(135deg, rgba(27, 29, 60, 0.8), rgba(54, 77, 116, 0.6)), var(--color-background);
  color: var(--vt-c-white);
}

.header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 16px;
  align-items: flex-end;
}

.header h1 {
  font-size: clamp(1.8rem, 3vw, 2.8rem);
  font-weight: 700;
  margin-bottom: 8px;
}

.tagline {
  font-size: 0.95rem;
  opacity: 0.9;
}

.endpoint {
  font-size: 0.85rem;
  background: rgba(255, 255, 255, 0.1);
  padding: 8px 12px;
  border-radius: 8px;
  display: flex;
  gap: 8px;
  align-items: center;
  word-break: break-all;
}

.endpoint a {
  color: inherit;
  text-decoration: underline;
}

.panels {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 24px;
  flex: 1;
}

.panel {
  background: rgba(255, 255, 255, 0.08);
  border-radius: 16px;
  padding: 24px;
  backdrop-filter: blur(8px);
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.panel h2 {
  font-size: 1.25rem;
  font-weight: 600;
}

.panel-hint {
  font-size: 0.9rem;
  opacity: 0.85;
}

.drop-zone {
  border: 2px dashed rgba(255, 255, 255, 0.4);
  border-radius: 12px;
  padding: 32px 24px;
  text-align: center;
  display: flex;
  flex-direction: column;
  gap: 12px;
  transition: border-color 0.25s ease, background 0.25s ease;
}

.drop-zone.drop-active {
  border-color: #90e0ef;
  background: rgba(144, 224, 239, 0.1);
}

.drop-zone .or {
  font-size: 0.8rem;
  opacity: 0.7;
}

.file-picker {
  display: inline-flex;
  justify-content: center;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  padding: 10px 18px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.15);
  cursor: pointer;
  transition: background 0.2s ease;
}

.file-picker:hover {
  background: rgba(255, 255, 255, 0.25);
}

.file-picker input {
  display: none;
}

.tip {
  font-size: 0.75rem;
  opacity: 0.6;
}

.file-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.file-item {
  background: rgba(0, 0, 0, 0.25);
  border-radius: 10px;
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.file-meta {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
}

.file-name {
  font-size: 1rem;
}

.file-size {
  font-size: 0.8rem;
  opacity: 0.7;
}

.file-status {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}

.hash code {
  word-break: break-all;
  background: rgba(255, 255, 255, 0.1);
  padding: 4px 8px;
  border-radius: 6px;
  font-size: 0.8rem;
}

.icon-button {
  border: none;
  background: transparent;
  color: rgba(255, 255, 255, 0.6);
  cursor: pointer;
  font-size: 1rem;
  line-height: 1;
  padding: 4px;
  border-radius: 50%;
  transition: background 0.2s ease, color 0.2s ease;
}

.icon-button:hover:enabled {
  color: #fff;
  background: rgba(255, 255, 255, 0.2);
}

.icon-button:disabled {
  cursor: not-allowed;
  opacity: 0.4;
}

.panel-placeholder,
.placeholder {
  opacity: 0.7;
  font-size: 0.9rem;
}

.panel-actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.primary,
.secondary {
  border: none;
  border-radius: 8px;
  padding: 10px 18px;
  font-weight: 600;
  cursor: pointer;
  transition: transform 0.12s ease, background 0.2s ease, opacity 0.2s ease;
}

.primary {
  background: #48cae4;
  color: #012a4a;
  align-self: flex-start;
}

.primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.primary:not(:disabled):hover {
  transform: translateY(-1px);
  background: #4fd3f4;
}

.secondary {
  background: rgba(255, 255, 255, 0.15);
  color: inherit;
}

.secondary:hover {
  background: rgba(255, 255, 255, 0.25);
}

.result-panel {
  min-height: 320px;
}

.result-body {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.vndb-id a {
  color: #caf0f8;
}

.loading {
  font-size: 0.9rem;
  opacity: 0.8;
}

.game-card {
  display: flex;
  gap: 16px;
  align-items: flex-start;
  background: rgba(0, 0, 0, 0.35);
  border-radius: 12px;
  padding: 16px;
}

.game-cover {
  width: 140px;
  border-radius: 10px;
  object-fit: cover;
}

.game-info {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.game-title {
  font-size: 1.2rem;
  font-weight: 700;
}

.game-meta {
  font-size: 0.9rem;
  opacity: 0.85;
}

.game-description {
  font-size: 0.9rem;
  line-height: 1.5;
  white-space: pre-wrap;
}

.error {
  color: #ffb3c1;
}

.feedback {
  font-size: 0.85rem;
}

.settings-fab {
  position: fixed;
  right: 24px;
  bottom: 24px;
  width: 48px;
  height: 48px;
  border-radius: 50%;
  border: none;
  background: #0096c7;
  color: #fff;
  font-size: 1.3rem;
  cursor: pointer;
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.25);
  transition: transform 0.15s ease, background 0.2s ease;
}

.settings-fab:hover {
  transform: translateY(-2px);
  background: #00b4d8;
}

.gallery-fab {
  position: fixed;
  right: 88px;
  bottom: 24px;
  width: 44px;
  height: 44px;
  border-radius: 50%;
  border: none;
  background: #006994;
  color: #fff;
  font-size: 1.1rem;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0,0,0,0.25);
  transition: transform 0.15s ease, background 0.2s ease;
}

.gallery-fab:hover {
  transform: translateY(-2px);
  background: #008fbf;
}

.dialog-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 16px;
  z-index: 1000;
}

.dialog {
  background: #1f2833;
  color: #fff;
  padding: 24px;
  border-radius: 12px;
  width: min(480px, 100%);
  display: flex;
  flex-direction: column;
  gap: 16px;
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.3);
}

.dialog h3 {
  font-size: 1.2rem;
  font-weight: 600;
}

.dialog-field {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.dialog-field input {
  border-radius: 8px;
  border: 1px solid rgba(255, 255, 255, 0.2);
  background: rgba(255, 255, 255, 0.1);
  padding: 10px 12px;
  color: #fff;
}

.dialog-field input:focus {
  outline: 2px solid #4fd3f4;
  border-color: transparent;
}

.dialog-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.flex-spacer {
  flex: 1;
}

@media (max-width: 768px) {
  .layout {
    padding: 24px 16px 80px;
  }

  .panel {
    padding: 20px;
  }

  .game-card {
    flex-direction: column;
    align-items: center;
  }

  .game-cover {
    width: 100%;
    max-width: 280px;
  }

  .settings-fab {
    right: 16px;
    bottom: 16px;
  }

  .gallery-fab {
    right: 76px;
    bottom: 16px;
  }
}
</style>
