const fs=require('fs');
const path=require('path');
const src=fs.readFileSync(path.join(process.argv[2] || path.resolve(__dirname, '../Backend~'), 'public/js/locales.js'),'utf8');
const reg={};
const I18N={register(l,d){reg[l]=d;return I18N;}};
const window={I18N};
new Function('window',src.replace(/\bw\.I18N/g,'window.I18N').replace(/\bw\.LOCALE_DICTS/g,'window.LD'))(window);
function flat(o,p,out){for(const k of Object.keys(o)){const v=o[k];const key=p?p+'.'+k:k;if(v&&typeof v==='object')flat(v,key,out);else if(typeof v==='function')out.push([key,null]);else out.push([key,String(v)]);}return out;}
const esc=s=>'"'+s.replace(/\\/g,'\\\\').replace(/"/g,'\\"').replace(/\n/g,'\\n').replace(/\r/g,'')+'"';
let cs='// 由 Backend~/public/js/locales.js 自動轉出（node Tools/genloc.js），請勿手改；要改字請改 Backend~ 的字典後重新產生。\nnamespace Snake3D\n{\n    public static class LocData\n    {\n';
for(const [name,l] of [['Zh','zh-Hant'],['En','en']]){
  const items=flat(reg[l],'',[]).filter(x=>x[1]!==null);
  cs+='        public static readonly string[] '+name+' =\n        {\n';
  cs+=items.map(x=>'            '+esc(x[0])+', '+esc(x[1])).join(',\n');
  cs+='\n        };\n';
}
cs+='    }\n}\n';
process.stdout.write(cs);
console.error(Object.keys(reg),flat(reg['zh-Hant'],'',[]).length, flat(reg['zh-Hant'],'',[]).filter(x=>x[1]===null).length);
