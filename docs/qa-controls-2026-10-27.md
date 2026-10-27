# 操作方式修正驗證

## 本次行為

- 設定改為「操作方式：依手指／依搖桿」，移除觸控按鈕自動／顯示／隱藏選項。
- 依手指：按住遊戲畫面，蛇頭朝手指投影到場地的位置前進；手指保持不動時，每幀依鏡頭與玩家位置重新計算。搖桿不顯示，加速鈕仍可多指操作。
- 依搖桿：搖桿固定於畫面底部，依裝置保留 16–26 CSS px 安全邊距；可選左下／右下，加速鈕置於另一側。搖桿區域外的點擊不轉向。後續手機尺寸調整見 `qa-mobile-layout-2026-10-27.md`。
- 手勢取消、失焦、切換操作方式、旋轉／版面改變及開啟彈窗時清除指標狀態。
- 舊版隱藏或浮動搖桿偏好轉為依手指；原本顯示的固定搖桿偏好轉為依搖桿。三語設定與教學同步更新。

## 根因與重現

原本 `touchOn` 同時控制觸控元件顯示與 `padDown` 是否接受輸入；選隱藏後直接拒絕手指事件。
修正前以 CDP 真實觸控重現，手機直向測試回報：`隱藏搖桿仍應跟隨手指轉向 0` assertion failed。
使用者進一步確認新需求為蛇頭朝手指位置前進與底部固定搖桿，因此以新操作方式取代原先的顯示開關。

## 驗證

環境：Windows、Microsoft Edge、SwiftShader WebGL、Node.js v22.23.2。專案宣告的 Node 26 本次未執行。

| 檢查 | 結果 |
| --- | --- |
| `npm --prefix client test` | 7 個檔案、57 項通過；含 4 種尺寸／鏡頭投影與 6 組設定遷移案例，既有完整一局／再開一局及鍵盤測試 |
| `npm --prefix client run build` | 正式建置成功 |
| `client/e2e/control-methods.mjs` | 手機 390×844、844×390；平板 820×1180、1180×820 通過手指點按／移動、無搖桿操作、取消後重用、彈窗隔離、底部左右搖桿與設定保存 |
| `client/e2e/control-methods.mjs phoneP,desktop` | 最終建置再驗手機按住手指時開啟設定的清理、桌機滑鼠底部搖桿、鍵盤轉向與依手指模式的滑鼠隔離，全部通過 |
| `client/e2e/touch.mjs` | 全部通過：多指轉向＋加速、放開加速、暫停清理、左右手、語言、設定保存、焦點返回 |
| `client/e2e/matrix.mjs phoneP,phoneL,tabletP,tabletL,desktop zh-Hant,zh-Hans,en` | 全部通過：三語 × 五種尺寸的首頁、設定、教學、單人挑戰、對戰電腦設定、倒數、遊戲中、暫停及窄版資訊欄；無版面檢查或瀏覽器錯誤 |
| `client/e2e/flow.mjs` | 全部通過：對戰電腦完整一局、結算 overlay、再來一局、單人挑戰結算、返回設定／首頁、連續重開四次與出局後觀戰切換 |
| `npm --prefix client test -- --run tests/i18n.test.js` | 最後教學文案更新後 8 項語系檢查通過 |
| `git diff --check` | 通過 |

瀏覽器一律使用正式建置。開發模式 React 重建 WebGL 時曾出現 shader precision 讀取 null，已將驗證切換為正式預覽；未將此開發模式問題當作觸控修正通過的證據。

最終有界巡檢：一項連貫的操作方式實作，隨後依新證據同步修正教學文案與操作方式相關設定顯示；沒有更動遊戲規則。正式測試網站使用 `APP_URL=http://127.0.0.1:4187/`，避免連到其他既有預覽服務。

## 畫面證據與清理

已檢視實際手機直橫向、平板直橫向與桌機畫面。必要截圖保留在 Git 忽略的本機輸出位置，可用 e2e 的 `SHOTS=1`／`SHOT_DIR` 重新產生：

- `.shots/control-methods/control-phoneP-finger.png`：手機依手指操作。
- `.shots/control-methods/control-phoneP-joystick-left.png`：手機底部固定搖桿。
- `.shots/control-methods/control-tabletL-joystick-right.png`：平板橫向右下搖桿。
- `.shots/control-final/control-phoneP-settings.png`：手機操作方式與底部左右設定，可捲到末端。
- 另保留已檢視的平板設定、手機橫向、平板橫向、桌機設定、英文設定及完整對局結算，共 10 張。

測試預覽已關閉；刪除本次重複與失敗截圖 159 張，釋放 36,015,635 bytes。原始碼、測試、正式建置與必要證據保留。

## 限制

- CDP 可驗證 Chromium 真實觸控事件與實際 3D 畫面；本次沒有實體 Android／iOS、Safari 實機驗證。
- 本次不變更規則與後端；正式網站部署與線上多人實際服務尚未驗證。
- 本機修改與測試不代表公開網站已更新。
