#!/usr/bin/env python3
"""由 shared/locales.json 的繁體（zh-Hant）原文，以 OpenCC（tw2sp，含兩岸用語）產生 zh-Hans 字典。
用法：pip install opencc-python-reimplemented && python scripts/gen-hans.py
新增 t('…') 字串後，先補 zh-Hant 與 en，再跑這支；個別用語不理想請寫進 OVERRIDES，不要手改 zh-Hans。"""
import json, os
import opencc

PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'shared', 'locales.json')
OVERRIDES = {  # key -> 手動指定的簡體
    'lang.zh-Hant': '繁體中文',
    'lang.zh-Hans': '简体中文',
    'lang.en': 'English',
    'fruit.1': '菠萝',
}
POST = [('缺省', '默认')]  # OpenCC 把「預設」轉成「缺省」，統一改回常用的「默认」

with open(PATH, encoding='utf8') as f:
    dicts = json.load(f)
hant = dicts.get('zh-Hant') or dicts['zh-TW']
cc = opencc.OpenCC('tw2sp')

def to_hans(key, text):
    if key in OVERRIDES:
        return OVERRIDES[key]
    v = cc.convert(text)
    for a, b in POST:
        v = v.replace(a, b)
    return v

hant['lang.zh-Hans'] = '简体中文'
en = dicts['en']
en['lang.zh-Hans'] = '简体中文'
out = {'zh-Hant': hant, 'zh-Hans': {k: to_hans(k, v) for k, v in hant.items()}, 'en': en}
with open(PATH, 'w', encoding='utf8', newline='\n') as f:
    json.dump(out, f, ensure_ascii=False, indent=1)
    f.write('\n')
print('zh-Hans：%d 筆' % len(out['zh-Hans']))
