# 3D貪吃蛇

可愛動物園風的 3D 貪吃蛇：單人挑戰、對戰電腦（幼幼班／簡單／普通／困難）、線上多人（大廳、房間、邀請連結、觀戰、聊天）。繁體中文／简体中文／English，觸控優先（手機、平板、桌機），桌機用鍵盤操作。

- 前端：**React 18 + Three.js**（Vite 建置）→ `client/`
- 後端：**Node.js** 權威房間伺服器（零第三方套件，自帶 WebSocket）→ `server/`
- 規則核心、電腦對手與語系字典：前後端共用 → `shared/`

## 快速開始

需要 Node.js 26.12.1 或更新的 26.x 修補版本。

```
npm run install:all          # 安裝前端套件（後端沒有第三方套件）
npm run build                # 建置前端到 client/dist
npm start                    # 啟動後端（預設 3130），順便提供 client/dist → http://localhost:3130
```

開發時分開跑：

```
npm run dev:server           # 後端 http://localhost:3130
cp client/.env.example client/.env.local     # GAME_SERVER_URL=http://localhost:3130
npm run dev                  # 前端 http://localhost:5173（Vite dev server，熱更新）
```

單機模式（單人挑戰、對戰電腦）完全在瀏覽器內執行，不需要後端，也不會連線；只有進入「線上多人」或開啟邀請連結才會連後端。

## 設定

| 位置 | 變數 | 說明 |
| --- | --- | --- |
| 前端（建置時） | `GAME_SERVER_URL` | 後端公開的 HTTP(S) 網址（可含連接埠與子路徑，不可含帳密、查詢參數或 `#`）。沒設定時線上模式顯示「尚未設定伺服器」。網址參數 `?server=` 可臨時覆寫。HTTPS 頁面必須搭配 HTTPS 後端。 |
| 後端 | `PORT` | 預設 3130 |
| 後端 | `GAME_ALLOWED_ORIGIN` | 允許連進來的前端網址（逗號分隔）。留空代表不限制，只適合本機。 |
| 後端 | `GAME_STATIC_DIR` | 選用：要順便提供的前端靜態檔資料夾，預設 `../client/dist` |

範例見 `.env.example`、`client/.env.example`、`server/.env.example`；不要把 `.env*` 實際檔案提交進 Git。

## 測試

```
npm test                     # server：規則核心、電腦對手、房間生命週期、端對端；client：vitest（設定、語系、連線層、場次）
```

瀏覽器 e2e（需要 Microsoft Edge；以軟體繪製 WebGL）：

```
npm run build
npm --prefix client run preview -- --port 4173   # 另一個終端機
npm start                                         # 另一個終端機（3130）
SHOT_DIR=./shots SHOTS=1 npm run e2e              # 尺寸矩陣（手機／平板直橫向／桌機 × 繁中／簡中／英文）＋線上流程，並存截圖
```

`SHOT_DIR` 與截圖都是可重建的產物，已被 `.gitignore` 忽略。

## 部署（預設拓撲）

- **前端 → GitHub Pages**：`.github/workflows/pages.yml` 在 push 到 `main` 時測試、建置並部署 `client/dist`。在 repository 設定 Actions variable `GAME_SERVER_URL`（Render 後端的 HTTPS 網址）。
- **後端 → Render**：`render.yaml`（Blueprint，`region: singapore`，Node 26.12.1）。在 Render 服務設定 `GAME_ALLOWED_ORIGIN` 為 Pages 網址（例如 `https://<帳號>.github.io`）。健康檢查 `/health`。
  - Render 免費層會休眠：進入線上流程時前端會先輪詢 `/health` 喚醒再連線；連不上超過 30 秒會提示。房間只存在記憶體，服務重啟後會消失。
  - Render 不支援修改既有服務的 region，更新 Blueprint 不會搬遷已建立的服務；要搬遷請建立新服務、複製環境變數，確認 `/health` 與線上對局可用後，再更新 `GAME_SERVER_URL` 並停用舊服務。
- 本專案不會自動 push、建立雲端服務或設定網域。

## 專案結構

```
client/    React + Three.js 前端
  src/ui/        自訂元件（按鈕、彈窗、開關、滑桿、分段選擇、下拉、輸入、捲動區與捲軸）
  src/screens/   首頁、說明、單機設定、大廳、房間、對戰（HUD、側欄、結算 overlay）
  src/game/      world.js（Three.js 舞台）、session.js（規則／快照／輸入）
  tests/ e2e/    vitest 單元測試、Edge 端對端
server/    房間、邀請、觀戰、聊天、權威對局、WebSocket
shared/    rules.js（確定性規則，固定 30 tick/s、種子亂數）、ai.js、locales.json
scripts/   gen-hans.py（由繁體字典以 OpenCC 產生簡體字典）
```

## 操作與規則

- 觸控：手指按在畫面任何地方拖曳（搖桿），按住「加速」鈕衝刺；設定裡可改成固定位置搖桿、換左右手。
- 鍵盤：方向鍵／WASD（可斜走）、Space／Shift 加速；Esc 開暫停選單。
- 吃果實變長變粗；撞到石頭、自己或別條蛇就出局，身體變成發光果實；碰到圍欄不會出局，但會扣掉加速能量。場上只有石頭會擋路；草叢與地面小物只是裝飾，可以直接穿過。最後存活者獲勝，有時限時分數最高者獲勝。

- 果實有 8 種外觀（蘋果、橘子、梨子、蜜桃、檸檬、李子、櫻桃、藍莓），分數與成長都一樣；另有金色星星（+30）與出局的蛇變成的發光果實（+5）。
- 12 種舞台（地圖）主題：動物園草地、糖果樂園、陽光沙灘、雪地森林、夜間動物園、竹林、沙漠綠洲、楓紅秋林、珊瑚海底、星空宇宙、櫻花公園、火山熔岩；各有不同的配色、草叢、地面小物與背景飄浮物。設定頁可指定舞台，預設為隨機。
- 加速能量條旁有閃電圖示；貼著圍欄會額外扣能量。
- 出局或觀戰時，點資訊欄（或上方分數列）的存活玩家（含電腦）可切換觀看視角。

## 語言、統計與大廳

- 語言：繁體中文、简体中文、English。右上角地球鈕切換，偏好存在共用 key `game-island-lang`，網址 `?lang=` 優先。字典在 `shared/locales.json`：新增文字時先補繁體與英文，再執行 `py scripts/gen-hans.py`（需 `pip install opencc-python-reimplemented`）產生簡體，不要手改簡體。
- GA4：沿用其他遊戲的事件規格（page_view、game_start、game_end、room_create、room_join、lobby_click），設定在 `client/index.html` 的 `GAME_ANALYTICS_CONFIG`；只在 `pjh-eric.github.io` 載入，本機與預覽不送資料，也不送姓名、房號或邀請 token。
- 首頁左上角「回遊戲大廳」連結到遊戲大廳並帶目前語言。

## 已知限制

- 美術與音效皆為程式產生的暫用資產（Three.js 基本幾何、WebAudio 合成），可替換成正式資源。
- 字型使用 Google Fonts（Baloo 2、Noto Sans TC），離線時退回系統字型。
- 房間狀態存在後端記憶體；斷線超過 30 秒視為離場。
