# 3D貪吃蛇（Unity 版）

可愛動物園風的 3D 貪吃蛇，Unity 6.3 LTS（6000.3.27f1）→ **只輸出 WebGL**。
本專案為唯一保留的貪吃蛇版本，使用 C# 規則核心與專案內的 Node.js 權威伺服器：單人挑戰、對戰電腦（簡單／普通／困難）、線上多人（大廳、房間、邀請連結、觀戰、聊天）。繁體中文／English。觸控優先（平板 > 桌機 > 手機），桌機用鍵盤操作（滑鼠不能操控蛇）。

## 開啟與執行

1. Unity Hub 加入此資料夾，使用 **6000.3.27f1** 開啟（第一次會匯入並產生 `.meta`，請連同 `.meta` 一起 commit）。
2. 首次開啟會自動套用專案設定（Gamma、WebGL 範本、舊版 Input 等）；也可手動執行選單 **Snake3D ▸ 套用專案設定**。
3. 開啟 `Assets/Scenes/Main.unity`（空殼場景；所有畫面在執行時由 `App.Boot` 產生）→ Play。
4. 線上模式需要伺服器：在編輯器內設環境變數 `GAME_SERVER_URL` 後啟動 Unity，或在網址加 `?server=https://...`（WebGL）。

## 建置 WebGL

- 選單：**Snake3D ▸ 建置 WebGL**，輸出到 `Build/WebGL`。
- 批次：
  ```
  GAME_SERVER_URL=https://your-service.onrender.com \
  Unity -batchmode -quit -projectPath . -executeMethod Snake3D.EditorTools.BuildTool.BuildWebGL -logFile -
  ```
  建置時把 `GAME_SERVER_URL` 寫入 `Assets/Resources/ServerUrl.txt`（已被 `.gitignore` 忽略，建置後自動刪除）。
- 不壓縮（Compression Disabled），GitHub Pages 可直接部署。

## 部署（預設拓撲）

- 前端：把 `Build/WebGL` 內容發佈到 GitHub Pages；建置時由 Actions repository variable `GAME_SERVER_URL` 注入。
- 後端：使用專案內 `Backend~/` 的 Node 伺服器（Render，`region: singapore`），設定 `GAME_ALLOWED_ORIGIN` 為 Pages 網址。Render 免費層會休眠，遊戲會先輪詢 `/health` 喚醒再連線。
- 本專案不會自動 push 或建立雲端服務；`.github/workflows` 需自行加入。

## 測試與檢查（不需要 Unity）

```
node CoreTests~/golden.js > CoreTests~/golden.json
dotnet run --project CoreTests~/CoreTests.csproj -- CoreTests~/golden.json # 規則與參照比對
dotnet build GameCheck~/GameCheck.csproj # 非 Unity 環境的型別檢查
dotnet build GameCheck~/GameCheck.csproj -p:DefineConstants=UNITY_WEBGL # WebGL 條件編譯的簽名檢查
dotnet run --project GameCheck~/InputTests/InputTests.csproj # 輸入、網址、喚醒狀態與 DPI／斷點計算
npm --prefix Backend~ test # 權威伺服器、房間與真實 WebSocket
```
`~` 結尾的資料夾 Unity 會忽略。比對資料須先產生並明確傳入 CoreTests；沒有提供時測試會註明略過，不能宣稱已完成比對。
GameCheck 的 Unity 替身主要供簽名檢查；InputTests 執行純 C# 狀態與數學計算，並以可控 HTTP 回應替身手動推進喚醒協程，不能取代真實 Unity 網路、Play Mode、WebGL 建置或畫面驗收。所有使用同一專案輸出目錄的 dotnet 指令請依序執行。

`GAME_SERVER_URL`／`?server=` 必須是有效的 HTTP(S) 基底網址，可包含連接埠及服務子路徑；不可包含帳密、查詢參數或 fragment，否則拼接 `/health` 與 `/ws` 無法正確運作。HTTPS 頁面必須搭配 HTTPS 後端。

2026-10-25 修復與未執行的畫面矩陣見 [QA 記錄](docs/qa-2026-10-25.md)。

## 專案結構

- `Assets/Scripts/Core`：確定性規則核心（固定 30 tick/s、種子亂數、AI），無 Unity 相依。
- `Assets/Scripts/Game`：3D 場景（程序建模）、自訂 uGUI 元件、頁面、網路、音效（程式合成）。
- `Assets/Plugins/WebGL/SnakeBridge.jslib`：WebSocket、分享、震動等瀏覽器橋接。
- `Assets/WebGLTemplates/SnakeTemplate`：全視窗畫布、載入條、SEO。
- `Assets/Scripts/Editor`：專案設定與建置腳本。

## 已知限制

- 字型為 Noto Sans CJK TC 子集（見 `THIRD_PARTY_NOTICES.md`）；子集外的暱稱字會顯示方框。
- WebGL 的 InputField 不支援完整 IME 組字體驗；手機輸入中文依系統鍵盤而異。
- 電腦對手在 C# 與 JS 之間因浮點末位差異，同一種子後期可能與伺服器參照走法分歧（規則不變，golden 測試容許此差異）。
- 美術與音效皆為程式產生的暫用資產，可替換成正式資源。

## 專案內伺服器與維護

以 `npm --prefix Backend~ start` 啟動權威伺服器（預設 3130）；只有 Unity/WebGL 是玩家介面，`Backend~/public/js/` 僅保留權威規則、AI 與語系來源，不含舊網頁版遊戲。Unity 載入 `GAME_SERVER_URL` 指向該伺服器；網址與跨來源限制依 `Backend~/.env.example` 設定。

`Library/`、`Temp/`、`Logs/`、`Build/`、測試的 `bin/`、`obj/`、產生的 golden 資料、截圖及快取均為可重建產物，測試後可清除；保留 `Assets/`、`.meta`、字型、音訊、正式圖片、`Packages/`、`ProjectSettings/` 與所有測試來源。Unity 介面目前提供繁體中文與英文。
