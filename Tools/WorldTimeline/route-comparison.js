/* Loaded after the dashboard's shared DOM and table helpers. */
(() => {
  const routes = ["matched", "spring", "rush"];
  const labels = ["旁观 · 孤松", "开泉 · 孤松", "主角抢剑 · 赤砾"];
  const outcomes = {Cleared:"通关", WorldDestroyed:"灭世", PlayerKilledChosen:"主角击杀天选", LifeRecorded:"本世留档", Running:"未结束"};
  let samples;
  let source;

  function readRows(text, required) {
    const lines = text.trimEnd().replace(/^\uFEFF/, "").split(/\r?\n/);
    const header = lines.shift().split("\t");
    if (!required.every(key => header.includes(key))) throw Error("路线文件缺少 " + required.filter(key => !header.includes(key)).join("、"));
    return lines.filter(Boolean).map((line, i) => {
      const values = line.split("\t");
      if (values.length !== header.length) throw Error("第 " + (i + 2) + " 行列数不符");
      return Object.fromEntries(header.map((key, j) => [key, values[j]]));
    });
  }
  function integer(value, label, minimum = 0) {
    const result = Number(value);
    if (!value.trim() || !Number.isSafeInteger(result) || result < minimum) throw Error(label + "无效");
    return result;
  }
  function load(battles, endings, label) {
    const seeds = new Map();
    function run(id) {
      const match = /^(matched|spring|rush)-(\d+)$/.exec(id);
      if (!match) return null;
      const seed = integer(match[2], "种子");
      if (!seeds.has(seed)) seeds.set(seed, new Map());
      const runs = seeds.get(seed);
      if (!runs.has(match[1])) runs.set(match[1], { fights:[] });
      return runs.get(match[1]);
    }
    for (const r of readRows(endings, ["run","year","outcome","springYear","springUsed"])) {
      const sample = run(r.run);
      if (!sample) continue;
      if (sample.outcome || !Object.hasOwn(outcomes, r.outcome)) throw Error("结局重复或无法识别：" + r.run);
      Object.assign(sample, {outcome:r.outcome, year:integer(r.year,"结局年份"),
        springYear:r.springYear === "" ? null : integer(r.springYear,"开泉年份"), uses:integer(r.springUsed,"用泉次数")});
    }
    for (const r of readRows(battles, ["run","year","side","q","r","name","hp","end","bursts"])) {
      const sample = run(r.run);
      if (!sample || !["player","chosen"].includes(r.side)) continue;
      sample.fights.push({side:r.side, q:integer(r.q,"横坐标",-128), r:integer(r.r,"纵坐标",-128), name:r.name,
        year:integer(r.year,"战斗年份"), hp:integer(r.hp,"进场气血"), end:integer(r.end,"出场气血"),
        bursts:integer(r.bursts,"截脉次数")});
    }
    if (!seeds.size) throw Error("未找到 matched、spring、rush 路线记录");
    const selected = Number(byId("route-seed").value);
    samples = new Map([...seeds].sort(([a],[b]) => a-b));
    source = label;
    byId("route-seed").replaceChildren(...[...samples.keys()].map(seed => {
      const option = el("option","种子 " + seed);
      option.value = seed;
      return option;
    }));
    if (samples.has(selected)) byId("route-seed").value = selected;
    renderOverview();
    renderStages();
  }
  function denText(sample) {
    if (!sample) return "缺少路线";
    const den = sample.fights.filter(f => f.side === "player" && f.q === 3 && f.r === -6);
    if (!den.length) return sample.outcome && sample.outcome !== "Running" ? "未到达" : "尚无记录";
    return sum(den, f => f.bursts) + " 次";
  }
  function renderOverview() {
    let paired = 0, baselineLosses = 0, rescued = 0;
    const rows = [];
    for (const [seed, runs] of samples) {
      const watch = runs.get("matched"), spring = runs.get("spring");
      let verdict = "缺少完整配对";
      if (watch?.outcome && spring?.outcome && watch.outcome !== "Running" && spring.outcome !== "Running") {
        if (spring.springYear == null) verdict = "未开泉";
        else {
          paired++;
          if (watch.outcome === "WorldDestroyed") baselineLosses++;
          if (watch.outcome === "WorldDestroyed" && spring.outcome === "Cleared") { rescued++; verdict = "开泉救回"; }
          else if (watch.outcome === "Cleared" && spring.outcome === "Cleared") verdict = "两线都通关";
          else verdict = "未救回";
        }
      }
      rows.push([button("种子 " + seed, () => { byId("route-seed").value = seed; renderStages(); }),
        ...routes.map(route => { const s = runs.get(route); return s?.outcome ? outcomes[s.outcome] + " · 第 " + s.year + " 年" : "缺少结局"; }),
        spring?.springYear != null ? "第 " + spring.springYear + " 年开泉 · 使用 " + spring.uses + " 次" : "—",
        verdict, routes.map(route => denText(runs.get(route))).join(" / ")]);
    }
    byId("route-source").textContent = source + "。可同时选择同一次模拟导出的 battles.tsv 和 ends.tsv。";
    byId("route-rescues").textContent = "开泉救回 " + rescued + " 局。共 " + paired + " 组有效配对，其中旁观灭世 " + baselineLosses + " 局。";
    table(byId("route-overview"),["种子",...labels,"泉眼","配对结果","伏兽窟截脉（旁观 / 开泉 / 抢剑）"],rows);
  }
  function renderStages() {
    const runs = samples.get(Number(byId("route-seed").value));
    const stages = new Map();
    for (const route of routes) {
      const seen = new Map();
      for (const f of runs.get(route)?.fights ?? []) {
        const key = [f.side,f.q,f.r].join("/");
        const occurrence = (seen.get(key) ?? 0) + 1;
        seen.set(key, occurrence);
        const id = key + "/" + occurrence;
        if (!stages.has(id)) stages.set(id, {f, occurrence, year:f.year, cells:new Map()});
        const stage = stages.get(id);
        stage.year = Math.min(stage.year, f.year);
        stage.cells.set(route, "第 " + f.year + " 年　" + f.hp + " → " + f.end +
          (f.side === "player" && f.q === 3 && f.r === -6 ? "　截脉 " + f.bursts + " 次" : ""));
      }
    }
    table(byId("route-stages"),["角色","关卡",...labels],[...stages.values()].sort((a,b) => a.year-b.year).map(s => [
      s.f.side === "player" ? "主角" : "天选", s.f.name + " (" + s.f.q + ", " + s.f.r + ")" + (s.occurrence > 1 ? " · 第 " + s.occurrence + " 战" : ""),
      ...routes.map(route => s.cells.get(route) ?? (runs.has(route) ? "无此战斗记录" : "缺少路线"))]));
  }
  function reset() {
    const snapshot = JSON.parse(byId("route-snapshot").textContent);
    load(snapshot.battles, snapshot.endings, snapshot.label);
  }
  byId("route-seed").addEventListener("change", renderStages);
  byId("route-reset").addEventListener("click", reset);
  byId("route-files").addEventListener("change", async event => {
    const files = [...event.target.files];
    event.target.value = "";
    try {
      const battle = files.find(f => f.name === "battles.tsv"), end = files.find(f => f.name === "ends.tsv");
      if (files.length !== 2 || !battle || !end) throw Error("请同时选择同批 battles.tsv 和 ends.tsv");
      const [battles, endings] = await Promise.all([battle.text(), end.text()]);
      load(battles, endings, "已导入路线记录");
    } catch (e) { byId("route-source").textContent = e.message + "。当前仍显示：" + source; }
  });
  reset();
})();

