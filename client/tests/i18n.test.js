import { beforeEach, describe, expect, it } from 'vitest';
import dicts from '../../shared/locales.json';
import { autoName, getLang, roomName, serverMessage, setLang, t } from '../src/i18n.js';

const params = s => [...s.matchAll(/\{(\w+)\}/g)].map(m => m[1]).sort().join(',');

describe('語系字典', () => {
  it('zh-Hant、zh-Hans 與 en 的 key 與 {參數} 完全一致', () => {
    const zh = Object.keys(dicts['zh-Hant']), en = Object.keys(dicts.en);
    expect(en.filter(k => !(k in dicts['zh-Hant']))).toEqual([]);
    expect(zh.filter(k => !(k in dicts.en))).toEqual([]);
    for (const k of zh) expect(params(dicts.en[k]), k).toBe(params(dicts['zh-Hant'][k]));
    expect(Object.keys(dicts['zh-Hans'])).toEqual(zh);
    for (const k of zh) expect(params(dicts['zh-Hans'][k]), k).toBe(params(dicts['zh-Hant'][k]));
  });

  describe('切換與保存', () => {
    beforeEach(() => setLang('zh-Hant'));
    it('預設繁中，切換後寫入 localStorage 並更新 <html lang>', () => {
      expect(getLang()).toBe('zh-Hant');
      setLang('en');
      expect(t('menu.solo')).toBe('Vs computer');
      expect(localStorage.getItem('game-island-lang')).toBe('en');
      expect(document.documentElement.lang).toBe('en');
    });
    it('簡體中文：字典、<html lang>、伺服器句子', () => {
      setLang('zh-Hans');
      expect(t('common.close')).toBe('关闭');
      expect(document.documentElement.lang).toBe('zh-Hans');
      expect(serverMessage('小花 加入了房間')).toBe(t('srv.joined', { name: '小花' }));
    });
    it('語言代碼正規化：zh-TW、zh-CN、en-US 都對得上，不支援的忽略', () => {
      setLang('zh-CN'); expect(getLang()).toBe('zh-Hans');
      setLang('zh-TW'); expect(getLang()).toBe('zh-Hant');
      setLang('en-US'); expect(getLang()).toBe('en');
      setLang('fr'); expect(getLang()).toBe('en');
    });
    it('參數替換、找不到的 key 回傳 key', () => {
      expect(t('res.time', { m: 1, s: 5 })).toContain('1');
      expect(t('no.such.key')).toBe('no.such.key');
    });
    it('伺服器的繁中系統句子在英文介面轉成英文', () => {
      setLang('en');
      expect(serverMessage('小花 加入了房間')).toBe('小花 joined the room');
      expect(serverMessage('房間已經沒有玩家，自動關閉了')).toBe(t('srv.autoClosed'));
      expect(serverMessage('完全沒見過的句子')).toBe('完全沒見過的句子');
      setLang('zh-Hant');
      expect(serverMessage('小花 加入了房間')).toBe('小花 加入了房間');
    });
    it('房名與自動暱稱跟著語言走', () => {
      setLang('en');
      expect(roomName('小花的房間')).toBe("小花's room");
      expect(autoName(0, 'cat')).toBe(t('adj.0') + t('name.sep') + t('animal.cat'));
      expect(autoName(0, 'cat')).toMatch(/^Happy /);
      setLang('zh-Hant');
      expect(autoName(0, 'cat')).toBe('快樂小貓');
    });
  });
});
