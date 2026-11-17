import fs from 'node:fs/promises';
import path from 'node:path';
import {Workbook,SpreadsheetFile} from '@oai/artifact-tool';
const root=path.resolve(import.meta.dirname,'../..');
const currentEvents = await fs.readFile(path.join(root, 'Client/Assets/GameMain/Data/DataTables/StoryEvent.txt'), 'utf8').catch(error => {
 if (error.code === 'ENOENT') return '';
 throw error;
});
if (currentEvents.includes('Timeline_StoryEvent_')) {
 throw new Error('Story text now lives in Language.xlsx. Edit the framework Excel tables and export them from Unity.');
}
const tables=[
 {name:'StoryEvent',title:'关卡剧情',fields:[['Id','int','编号'],['Q','int','横坐标'],['R','int','纵坐标'],['Title','string','标题'],['Text','string','剧情'],['Background','string','背景'],['Repeatable','int','可重访']],rows:[
 [1,1,-9,'古修石门','山壁嵌着一扇无锁石门。掌印深陷门中，石缝透出微弱剑鸣。唯有气血足够雄厚者，才能推开这道门。','Gate',1],
 [2,-1,-9,'枯井残卷','古井已经干涸，井底却有一线青光。你循着石阶而下，看见一枚玉佩压着几页散落的旧纸。','Well',0],
 [3,-1,-8,'濒死散修','散修倚在松根旁，气息渐弱。他握着一卷书，低声道：若有青髓玉护住心脉，或许还能活着走出这片山林。','Traveler',0],
 [4,0,-9,'黑牙巢遗物','妖兽倒下后，洞中终于安静。碎骨之间露出一只石匣，匣盖上的青色纹路仍在微微发亮。','Cave',0],
 [5,0,-8,'断桥遗阵','山涧截断了旧路。桥头残阵还留着一道接口，也许稍耗气血便能续上阵纹，替后来者铺出一条通途。','Bridge',1],
 [6,-9,0,'青禾旧簿','渡船簿上记着十二人。船夫数到十一便停了。他用指腹抹过纸上的空行，低声说：临溪那头还有一本底簿。','Bridge',0],
 [7,4,-9,'临溪渡口','船夫沈砚在空船上摆了两只碗。他说自己曾替妹妹刮去一个名字。说完他盯着第二只碗，像是忘了刚才提起的是谁。','Bridge',0],
 [8,3,-9,'沉舟寻铃','船舱横在回流里，缆绳一紧一松。断桨上刻着沈砚的名字，舱底那枚铃却一声也没响。','Cave',0]]},
 {name:'StoryOption',title:'剧情分支',fields:[['Id','int','编号'],['EventId','int','事件编号'],['Text','string','选项'],['Condition','string','条件'],['Value','int','条件值'],['Flag','string','条件标记'],['Hint','string','条件说明'],['Weight','int','自动权重'],['ItemId','int','奖励道具'],['CostItemId','int','消耗道具'],['HealthCost','int','消耗气血'],['Heal','int','恢复气血'],['Experience','int','经验'],['TimeYears','int','驻留年份'],['SetFlag','string','写入标记'],['Result','string','结果文本']],rows:[
 [101,1,'运转气血，推开石门','MaxHealthGreater',500,'','需要最大生命 > 500',4,9102,0,0,0,0,0,'','石门缓缓移开。你从石台上取下一柄古剑，剑身虽残，锋芒未尽。'],
 [102,1,'记下掌印，暂且离开','Always',0,'','',1,0,0,0,0,0,0,'','你记下石门的纹路，决定来日气血充盈时再试。'],
 [201,2,'下井查看青光','Always',0,'','',5,9101,0,0,0,0,0,'','青髓玉佩入手温润，灵气顺着经脉流转。旧纸早已朽坏，唯有玉佩保存完好。'],
 [202,2,'查看井边残字后离开','Always',0,'','',1,0,0,0,0,8,0,'','井壁记着一段吐纳心得。你默记片刻，转身离去。'],
 [301,3,'以青髓玉佩救人','Always',0,'','',5,9103,9101,0,0,0,0,'traveler_saved','玉佩护住了他的心脉。他将《敛息诀》交给你，低声说：此恩不忘。'],
 [302,3,'取走残卷，独自离开','Always',0,'','',1,9103,0,15,0,0,0,'','你伸手取卷，残留的护身灵气划伤手臂。身后的呼吸声渐渐低了下去。'],
 [303,3,'留下清水，继续赶路','Always',0,'','',2,0,0,0,0,5,0,'','你将水囊放在他手边，记下这片松林的位置。山风吹过，前路仍在。'],
 [401,4,'打开石匣','Always',0,'','',5,9101,0,0,0,0,0,'','石匣里躺着一枚青髓玉佩。妖兽不识此物，倒让它躲过了岁月的侵蚀。'],
 [402,4,'辨认洞中旧阵','Always',0,'','',1,0,0,0,0,20,0,'','你从破碎阵纹中悟出少许运气之法。洞中已无别的值得停留之物。'],
 [501,5,'续接桥头残阵','FlagUnset',0,'bridge_repaired','桥已被前人修复',4,0,0,10,0,12,1,'bridge_repaired','你以气血续上阵纹，守了一年。薄光终于跨过山涧，旧桥重新显出轮廓。后来者也能借此通行。'],
 [502,5,'踏上前人修复的桥','FlagSet',0,'bridge_repaired','尚无人修复此桥',6,0,0,0,0,0,0,'','阵桥稳稳托住脚步。桥头有一道新刻痕，不知是谁曾在此驻留。'],
 [503,5,'沿山涧绕行','Always',0,'','',1,0,0,0,0,0,2,'','你沿着山涧寻找落脚之处，花了两年才绕过这段险路。'],
 [601,6,'对照舱里的旧船牌','Always',0,'','',1,0,0,0,0,0,0,'','船牌刻着十二道痕。最后一道比其余的浅，像有人试着刮去它。'],
 [701,7,'问他第二只碗留给谁','Always',0,'','',1,0,0,0,0,0,0,'','沈砚没答。他望向西南的回水湾，说旧船沉在那里，船上或许还留着她认得的东西。'],
 [801,8,'解开缆绳，取出旧铃','Always',0,'','',4,9104,0,0,0,0,1,'','水下的铃一出船舱，回流便停了一息。铃身刻着临溪两字。'],
 [802,8,'沿断桨返回岸上','Always',0,'','',1,0,0,0,0,0,0,'','你记住了进舱的路线。岸上还留着两行脚印，其中一行在水边断了。']]},
 {name:'StoryItem',title:'剧情道具与被动',fields:[['Id','int','编号'],['Name','string','名称'],['Description','string','效果'],['AttackBonus','int','攻击加成'],['HealthBonus','int','生命加成'],['CombatAttackPercent','int','战斗攻击百分比']],rows:[
 [9101,'青髓玉佩','最大生命 +80，获得时同时增加80当前生命；可用于救助散修。',0,80,0],
 [9102,'古修残剑','攻击 +12；升级后仍保留此加成。',12,0,0],
 [9103,'敛息诀','被动：战斗攻击提高15%。',0,0,15],
 [9104,'镇水铃','从临溪沉舟中寻回的旧铃。铃声响时，回流会短暂停歇。',0,0,0]]}
];
function col(n){let s='';for(;n>0;n=Math.floor((n-1)/26))s=String.fromCharCode(65+(n-1)%26)+s;return s;}
function seven(n){let u=n>>>0, a=[];while(u>=128){a.push((u&127)|128);u>>>=7;}a.push(u);return Buffer.from(a);}
for(const table of tables){
 const wb=Workbook.create(), sheet=wb.worksheets.add(table.name);
 const matrix=[['#',table.title,...Array(table.fields.length-1).fill('')],['#',...table.fields.map(x=>x[0])],['#',...table.fields.map(x=>x[1])],['#',...table.fields.map(x=>x[2])],...table.rows.map(row=>['',...row])];
 const end=col(table.fields.length+1),range=`A1:${end}${matrix.length}`;
 sheet.getRange(range).values=matrix;sheet.showGridLines=false;
 sheet.getRange(range).format.font.name='Microsoft YaHei';sheet.getRange(range).format.font.size=11;
 sheet.getRange(range).format.columnWidth=16;sheet.getRange(range).format.rowHeight=28;
 sheet.getRange(`A1:${end}4`).format.fill='#223239';sheet.getRange(`A1:${end}4`).format.font.color='#F3E8CD';
 sheet.getRange('A:A').format.columnWidth=4;sheet.freezePanes.freezeRows(4);
 for(let i=0;i<table.fields.length;i++){
   sheet.getRange(`${col(i+2)}1:${col(i+2)}${matrix.length}`).format.columnWidth=Math.max(16,table.fields[i][0].length+4);
   if(table.fields[i][1]==='int')sheet.getRange(`${col(i+2)}2:${col(i+2)}${matrix.length}`).format.horizontalAlignment='center';
 }
 for(let i=0;i<table.fields.length;i++)if(['Text','Result','Description','Hint'].includes(table.fields[i][0])){
   sheet.getRange(`${col(i+2)}1:${col(i+2)}${matrix.length}`).format.columnWidth=55;
   sheet.getRange(`${col(i+2)}5:${col(i+2)}${matrix.length}`).format.wrapText=true;
 }
 sheet.getRange(`A5:${end}${matrix.length}`).format.rowHeight=65;
 wb.recalculate();
 console.log((await wb.inspect({kind:'table',range:`${table.name}!A1:${col(Math.min(8,table.fields.length+1))}${Math.min(7,matrix.length)}`,include:'values',tableMaxRows:7,tableMaxCols:8})).ndjson);
 await (await SpreadsheetFile.exportXlsx(wb)).save(path.join(root,'Config',table.name+'.xlsx'));
 const text=matrix.map(row=>row.join('\t')).join('\r\n')+'\r\n';
 const folder=path.join(root,'Client/Assets/GameMain/Data/DataTables');
 await fs.writeFile(path.join(folder,table.name+'.txt'),text,'utf8');
 let records=[];
 for(const row of table.rows){let cells=[];row.forEach((value,i)=>{if(table.fields[i][1]==='int')cells.push(seven(value));else{const b=Buffer.from(value,'utf8');cells.push(seven(b.length),b);}});const body=Buffer.concat(cells);records.push(seven(body.length),body);}
 await fs.writeFile(path.join(folder,table.name+'.bytes'),Buffer.concat(records));
 const declarations=table.fields.slice(1).map(([name,type])=>`        public ${type} ${name} { get; private set; }`).join('\n');
 const parse=table.fields.map(([name,type],i)=>`            ${i===0?'m_Id':name} = ${type==='int'?`int.Parse(cells[${i+1}])`:`cells[${i+1}]`};`).join('\n');
 const binary=table.fields.map(([name,type],i)=>`                ${i===0?'m_Id':name} = reader.${type==='int'?'Read7BitEncodedInt32()':'ReadString()'};`).join('\n');
 await fs.writeFile(path.join(root,`Client/Assets/GameMain/Scripts/DataTable/DR${table.name}.cs`),`// Generated from Config/${table.name}.xlsx by Tools/Tables/build_story_tables.mjs\nusing System.IO;\nusing System.Text;\nusing UnityGameFramework.Runtime;\nnamespace DataTable\n{\n    public sealed class DR${table.name} : DataRowBase\n    {\n        private int m_Id;\n        public override int Id => m_Id;\n${declarations}\n        public override bool ParseDataRow(string value, object userData)\n        {\n            var cells = value.Split(DataTableExtension.DataSplitSeparators);\n            for(int i=0;i<cells.Length;i++) cells[i]=cells[i].Trim(DataTableExtension.DataTrimSeparators);\n${parse}\n            return true;\n        }\n        public override bool ParseDataRow(byte[] bytes, int startIndex, int length, object userData)\n        {\n            using(var stream = new MemoryStream(bytes,startIndex,length,false))\n            using(var reader = new BinaryReader(stream,Encoding.UTF8))\n            {\n${binary}\n            }\n            return true;\n        }\n    }\n}\n`);
 try {const image=await wb.render({sheetName:table.name,range:`B2:${col(Math.min(5,table.fields.length+1))}${Math.min(7,matrix.length)}`,scale:1,format:'png'});await fs.writeFile(path.join(root,`Tests/WorldTimeline/obj/${table.name}-preview.png`),new Uint8Array(await image.arrayBuffer()));}catch(error){console.log('Preview unavailable: '+error.message);}
}
await fs.writeFile(path.join(root,'Tests/WorldTimeline/obj/story-tables.json'),JSON.stringify(tables,null,2));
