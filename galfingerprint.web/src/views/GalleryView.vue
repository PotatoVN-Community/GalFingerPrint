<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()

interface VndbGame {
  title: string
  description: string | null
  developers: string[]
  released: string | null
  imageUrl: string | null
}

interface RecordedGame {
  vndbId: string
  title?: string
  imageUrl?: string
  loading: boolean
  error?: string
  details?: VndbGame | null
}

interface GameHashVoteDto {
  hash: string
  count: number
  isDominant: boolean
}

interface GameVotesDtoFront {
  vndbId: string
  items: GameHashVoteDto[]
}

const defaultBackendUrl = 'https://dinzhen.api.potatovn.net'
const storageKey = 'galfingerprint.backendUrl'

const backendUrl = ref(defaultBackendUrl)

if (typeof window !== 'undefined') {
  const saved = window.localStorage.getItem(storageKey)
  if (saved) {
    backendUrl.value = saved
  }
}

const recordedGames = ref<RecordedGame[]>([])
const recordedLoading = ref(false)
const recordedError = ref<string | null>(null)
const recordedPage = ref(1)
const recordedPageSize = ref(40)

const selectedGame = ref<{
  vndbId: string
  vndbData?: VndbGame | null
  votes?: GameVotesDtoFront | null
  loading: boolean
  error?: string | null
} | null>(null)

onMounted(() => {
  loadRecordedGames()
})

function buildEndpoint(path: string) {
  const base = backendUrl.value.trim().replace(/\/+$/, '') || defaultBackendUrl
  const suffix = path.startsWith('/') ? path : `/${path}`
  return `${base}${suffix}`
}

async function safeReadText(response: Response) {
  try {
    return (await response.text()) || response.statusText
  } catch {
    return response.statusText
  }
}

async function loadRecordedGames(page = recordedPage.value, pageSize = recordedPageSize.value) {
  recordedLoading.value = true
  recordedError.value = null
  try {
    const resp = await fetch(buildEndpoint(`/game?page=${page}&pageSize=${pageSize}`))
    if (!resp.ok) {
      const msg = await safeReadText(resp)
      recordedError.value = `后端请求失败：${resp.status} ${msg}`
      return
    }
    const payload = (await resp.json()) as { items?: Array<{ vndbId?: string }>; total?: number }
    const items = payload.items ?? []
    recordedGames.value = items.map((i) => ({
      vndbId: i.vndbId ?? '',
      title: i.vndbId ?? '',
      imageUrl: undefined,
      loading: true,
      error: undefined,
      details: null,
    }))

    // 为每个条目并行获取 VNDB 简要信息（封面 / 标题）
    for (const g of recordedGames.value) {
      fetchVndbOverview(g)
    }
  } catch (err) {
    recordedError.value = `请求异常：${(err as Error).message}`
  } finally {
    recordedLoading.value = false
  }
}

async function fetchVndbOverview(g: RecordedGame) {
  g.loading = true
  g.error = undefined
  try {
    const response = await fetch('https://api.vndb.org/kana/vn', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        filters: ['id', '=', g.vndbId],
        fields: 'title,image.url',
      }),
    })
    if (!response.ok) {
      g.error = `VNDB 请求失败：${response.status}`
      return
    }
    const payload = (await response.json()) as { results?: Array<{ title?: string; image?: { url?: string | null } | null }> }
    const result = payload.results?.[0]
    if (!result) {
      g.error = 'VNDB 未返回结果'
      return
    }
    g.title = result.title ?? g.vndbId
    g.imageUrl = result.image?.url ?? undefined
    g.details = {
      title: result.title ?? g.vndbId,
      description: null,
      developers: [],
      released: null,
      imageUrl: result.image?.url ?? null,
    }
  } catch (err) {
    g.error = `VNDB 请求异常：${(err as Error).message}`
  } finally {
    g.loading = false
  }
}

async function openGameDetails(vndbIdStr: string) {
  selectedGame.value = { vndbId: vndbIdStr, loading: true }
  selectedGame.value!.error = null
  selectedGame.value!.vndbData = null
  selectedGame.value!.votes = null

  try {
    const resp = await fetch(buildEndpoint(`/game/byid/${encodeURIComponent(vndbIdStr)}`))
    if (resp.status === 404) {
      selectedGame.value!.error = '后端未找到该游戏的哈希信息'
      selectedGame.value!.loading = false
      return
    }
    if (!resp.ok) {
      const msg = await safeReadText(resp)
      selectedGame.value!.error = `后端请求失败：${resp.status} ${msg}`
      selectedGame.value!.loading = false
      return
    }

    const votes = (await resp.json()) as GameVotesDtoFront
    selectedGame.value!.votes = votes

    // 尝试使用已缓存的 VNDB 简要信息，否则请求完整详情
    const cached = recordedGames.value.find((x) => x.vndbId === vndbIdStr)
    if (cached?.details) {
      selectedGame.value!.vndbData = cached.details
    } else {
      const response = await fetch('https://api.vndb.org/kana/vn', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          filters: ['id', '=', vndbIdStr],
          fields: 'title,description,image.url,released,developers.name',
        }),
      })
      if (response.ok) {
        const payload = (await response.json()) as { results?: Array<any> }
        const r = payload.results?.[0]
        if (r) {
          selectedGame.value!.vndbData = {
            title: r.title ?? vndbIdStr,
            description: sanitizeDescription(r.description ?? null),
            developers: (r.developers ?? []).map((d: any) => d?.name).filter((n: any) => Boolean(n)),
            released: r.released ?? null,
            imageUrl: r.image?.url ?? null,
          }
        }
      }
    }
  } catch (err) {
    selectedGame.value!.error = `请求异常：${(err as Error).message}`
  } finally {
    if (selectedGame.value) selectedGame.value.loading = false
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

function closeSelectedGame() {
  selectedGame.value = null
}

function goBack() {
  router.push('/')
}
</script>

<template>
  <div class="layout">
    <header class="header">
      <div>
        <h1>已记录游戏</h1>
        <p class="tagline">数据库中已记录的游戏，点击海报查看哈希信息。</p>
      </div>
      <button class="back-button" type="button" @click="goBack">← 返回</button>
    </header>

    <main class="gallery-content">
      <div v-if="recordedLoading" class="loading">正在加载已记录游戏...</div>
      <p v-else-if="recordedError" class="error">{{ recordedError }}</p>

      <div v-else class="poster-grid">
        <div
          v-for="g in recordedGames"
          :key="g.vndbId"
          class="poster-card"
          role="button"
          @click="openGameDetails(g.vndbId)"
          :title="g.title"
        >
          <div class="poster-media">
            <img v-if="g.imageUrl" :src="g.imageUrl" :alt="g.title" class="poster-image" />
            <div v-else class="poster-placeholder">{{ g.title ? g.title.charAt(0) : '?' }}</div>
          </div>
          <div class="poster-caption">{{ g.title }}</div>
        </div>
      </div>
    </main>

    <!-- 游戏详情模态 -->
    <div v-if="selectedGame" class="dialog-backdrop" @click.self="closeSelectedGame">
      <div class="dialog" role="dialog" aria-modal="true">
        <button class="icon-button close-button" type="button" @click="closeSelectedGame">✕</button>

        <div class="game-card">
          <img
            v-if="selectedGame.vndbData?.imageUrl"
            :src="selectedGame.vndbData.imageUrl"
            alt="游戏封面"
            class="game-cover"
          />
          <div class="game-info">
            <h3 class="game-title">{{ selectedGame.vndbData?.title ?? selectedGame.vndbId }}</h3>
            <p v-if="selectedGame.vndbData?.released" class="game-meta">发售日期：{{ selectedGame.vndbData.released }}</p>
            <p v-if="selectedGame.vndbData?.developers?.length" class="game-meta">
              开发商：{{ selectedGame.vndbData.developers.join('、') }}
            </p>
            <p v-if="selectedGame.vndbData?.description" class="game-description">{{ selectedGame.vndbData.description }}</p>
          </div>
        </div>

        <div v-if="selectedGame.loading" class="loading">正在加载哈希信息...</div>
        <p v-else-if="selectedGame.error" class="error">{{ selectedGame.error }}</p>

        <div v-else-if="selectedGame.votes && selectedGame.votes.items?.length">
          <h4 class="hash-section-title">哈希信息</h4>
          <table class="hash-table">
            <thead><tr><th>哈希</th><th>计数</th><th>主导</th></tr></thead>
            <tbody>
              <tr v-for="it in selectedGame.votes.items" :key="it.hash">
                <td><code>{{ it.hash }}</code></td>
                <td>{{ it.count }}</td>
                <td><span v-if="it.isDominant">✓</span></td>
              </tr>
            </tbody>
          </table>
        </div>

        <p v-else class="placeholder">没有哈希记录</p>
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

.back-button {
  border: none;
  border-radius: 8px;
  padding: 10px 18px;
  font-weight: 600;
  background: rgba(255, 255, 255, 0.15);
  color: inherit;
  cursor: pointer;
  transition: background 0.2s ease;
}

.back-button:hover {
  background: rgba(255, 255, 255, 0.25);
}

.gallery-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.loading {
  font-size: 0.9rem;
  opacity: 0.8;
  text-align: center;
  padding: 40px;
}

.error {
  color: #ffb3c1;
  text-align: center;
  padding: 40px;
}

.poster-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 16px;
  align-items: start;
}

.poster-card {
  background: rgba(255, 255, 255, 0.03);
  border-radius: 10px;
  padding: 8px;
  text-align: center;
  cursor: pointer;
  transition: transform 0.12s ease, box-shadow 0.12s ease;
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-items: center;
}

.poster-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.35);
}

.poster-media {
  width: 100%;
  aspect-ratio: 3/4;
  overflow: hidden;
  border-radius: 8px;
}

.poster-image {
  width: 100%;
  height: 100%;
  object-fit: cover;
  background: #111;
}

.poster-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.06);
  font-size: 2rem;
  color: rgba(255, 255, 255, 0.8);
}

.poster-caption {
  font-size: 0.9rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  width: 100%;
}

.dialog-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.7);
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
  width: min(800px, 96%);
  max-height: 90vh;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 16px;
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.3);
  position: relative;
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

.icon-button:hover {
  color: #fff;
  background: rgba(255, 255, 255, 0.2);
}

.close-button {
  position: absolute;
  top: 16px;
  right: 16px;
  font-size: 1.5rem;
  padding: 8px;
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
  flex-shrink: 0;
}

.game-info {
  display: flex;
  flex-direction: column;
  gap: 10px;
  flex: 1;
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

.hash-section-title {
  font-size: 1.1rem;
  font-weight: 600;
  margin-top: 8px;
}

.hash-table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 12px;
}

.hash-table th,
.hash-table td {
  padding: 8px;
  text-align: left;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.hash-table code {
  word-break: break-all;
  background: rgba(255, 255, 255, 0.03);
  padding: 4px 6px;
  border-radius: 6px;
  font-size: 0.85rem;
}

.placeholder {
  opacity: 0.7;
  font-size: 0.9rem;
  text-align: center;
  padding: 20px;
}

@media (max-width: 768px) {
  .layout {
    padding: 24px 16px;
  }

  .poster-grid {
    grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
    gap: 12px;
  }

  .game-card {
    flex-direction: column;
    align-items: center;
  }

  .game-cover {
    width: 100%;
    max-width: 280px;
  }
}
</style>
