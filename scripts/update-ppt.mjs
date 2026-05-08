/**
 * update-ppt.mjs  v3
 * Works from: PropelIQ_Mentor_v2.pptx  (original)
 * Output:     PropelIQ_Mentor_v2_final.pptx
 *
 * Changes:
 *   1. Slide 5  — remove "Register with email / phone + OTP" from Patient column
 *   2. Slide 10 — ONE narrative slide (before Thank You):
 *                 "Challenges Faced During Development"
 *                 left: what went wrong (scenarios, API, query, env failures)
 *                 right: how we fixed it (raised bugs, root-caused, resolved)
 *
 * NO two-slide table layout. NO bug IDs. Clean narrative only.
 */

import { createRequire } from 'module';
const require = createRequire(import.meta.url);
const AdmZip = require('adm-zip');
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const PPTX_IN  = resolve(__dirname, '../PropelIQ_Mentor_v2.pptx');
const PPTX_OUT = resolve(__dirname, '../PropelIQ_Mentor_v2_final.pptx');

// ─── Namespaces ───────────────────────────────────────────────────────────────
const NS = [
  'xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"',
  'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"',
  'xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"',
].join(' ');

// ─── XML primitives ───────────────────────────────────────────────────────────
function esc(s) {
  return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function font(sz, bold, color) {
  const b = bold ? ' b="1"' : '';
  return (
    `<a:rPr lang="en-US" sz="${sz}"${b} dirty="0">` +
    `<a:solidFill><a:srgbClr val="${color}"/></a:solidFill>` +
    `<a:latin typeface="Segoe UI" panose="020B0502040204020203" pitchFamily="34" charset="0"/>` +
    `<a:ea typeface="Segoe UI" panose="020B0502040204020203" pitchFamily="34" charset="-122"/>` +
    `<a:cs typeface="Segoe UI" panose="020B0502040204020203" pitchFamily="34" charset="-120"/>` +
    `</a:rPr>`
  );
}

function para(text, sz, bold, color, spcPts, indentEmu) {
  spcPts    = spcPts    || 0;
  indentEmu = indentEmu || 0;
  const mar = indentEmu > 0
    ? ` marL="${indentEmu}" indent="-${indentEmu}"`
    : ' marL="0" indent="0"';
  const spc = spcPts > 0 ? `<a:spcAft><a:spcPts val="${spcPts}"/></a:spcAft>` : '';
  return `<a:p><a:pPr${mar}><a:buNone/>${spc}</a:pPr><a:r>${font(sz, bold, color)}<a:t>${esc(text)}</a:t></a:r></a:p>`;
}

function bullet(text, sz, color, spcPts, indentEmu) {
  spcPts    = spcPts    || 700;
  indentEmu = indentEmu || 342900;
  return (
    `<a:p>` +
    `<a:pPr marL="${indentEmu}" indent="-${indentEmu}">` +
    `<a:spcAft><a:spcPts val="${spcPts}"/></a:spcAft>` +
    `<a:buSzPct val="100000"/><a:buChar char="&#x2022;"/>` +
    `</a:pPr>` +
    `<a:r>${font(sz, false, color)}<a:t>${esc(text)}</a:t></a:r>` +
    `<a:endParaRPr lang="en-US" sz="${sz}" dirty="0"/>` +
    `</a:p>`
  );
}

function textBox(id, name, x, y, cx, cy, anchor, content) {
  return (
    `<p:sp>` +
    `<p:nvSpPr><p:cNvPr id="${id}" name="${name}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>` +
    `<p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${cx}" cy="${cy}"/></a:xfrm>` +
    `<a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/></p:spPr>` +
    `<p:txBody><a:bodyPr wrap="square" rtlCol="0" anchor="${anchor}"/><a:lstStyle/>${content}</p:txBody>` +
    `</p:sp>`
  );
}

function solidRect(id, name, x, y, cx, cy, fill, border, roundAdj) {
  const geom = roundAdj != null
    ? `<a:prstGeom prst="roundRect"><a:avLst><a:gd name="adj" fmla="val ${roundAdj}"/></a:avLst></a:prstGeom>`
    : `<a:prstGeom prst="rect"><a:avLst/></a:prstGeom>`;
  const ln = border
    ? `<a:ln w="22860"><a:solidFill><a:srgbClr val="${border}"/></a:solidFill><a:prstDash val="solid"/></a:ln>`
    : '';
  return (
    `<p:sp>` +
    `<p:nvSpPr><p:cNvPr id="${id}" name="${name}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>` +
    `<p:spPr><a:xfrm><a:off x="${x}" y="${y}"/><a:ext cx="${cx}" cy="${cy}"/></a:xfrm>` +
    `${geom}<a:solidFill><a:srgbClr val="${fill}"/></a:solidFill>${ln}</p:spPr>` +
    `</p:sp>`
  );
}

function slideRels() {
  return (
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
    `<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">` +
    `<Relationship Id="rId1" ` +
    `Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" ` +
    `Target="../slideLayouts/slideLayout1.xml"/>` +
    `</Relationships>`
  );
}

// ─── Single challenge slide ───────────────────────────────────────────────────
function buildChallengeSlide() {
  const W   = 12192000;
  const H   = 6858000;
  const PAD = 457200;
  const INN = 182880;
  const COL_W = 5532240;
  const COL_H = 4750000;
  const COL_Y = 1820000;
  const L_X   = PAD;
  const R_X   = W - PAD - COL_W;

  const leftBullets =
    bullet('Many application scenarios did not work as expected during end-to-end testing', 1150, '94A3B8', 720) +
    bullet('API endpoints returned HTTP 500 errors, empty results, or linked records to the wrong patient', 1150, '94A3B8', 720) +
    bullet('Frontend and backend mismatches caused features to silently break with no visible error', 1150, '94A3B8', 720) +
    bullet('Calendar and booking flows failed due to query issues, expired data, and library conflicts', 1150, '94A3B8', 720) +
    bullet('Dev-environment problems — DLL locks, missing role checks, LINQ translation errors — blocked the build', 1150, '94A3B8', 720) +
    bullet('AI assistant task checklists were not written back to the file after implementation', 1150, '94A3B8', 720);

  const rightBullets =
    bullet('Every failure was raised as a structured bug report in docs/bugs/ with a clear problem statement', 1150, '86EFAC', 720) +
    bullet('Root cause was traced through API logs, EF Core query output, and browser console errors', 1150, '86EFAC', 720) +
    bullet('Each bug was resolved with a targeted code fix — no workarounds, no loose ends left behind', 1150, '86EFAC', 720) +
    bullet('13 bugs were raised and fully resolved across backend, frontend, database, and dev environment', 1150, '86EFAC', 720) +
    bullet('Bug reports created a clear audit trail: what broke, why it broke, and exactly how it was fixed', 1150, '86EFAC', 720) +
    bullet('Every failure became a learning — the platform is more robust and reliable because of this process', 1150, '86EFAC', 720);

  const shapes = [
    solidRect(2,  'BG',      0,           0,           W,              H,       '0F172A'),
    solidRect(3,  'Accent',  PAD,         850000,      2200000,        45720,   'F97316'),
    solidRect(4,  'LCard',   L_X,         COL_Y,       COL_W,          COL_H,   '1E293B', 'F97316', 2000),
    solidRect(5,  'RCard',   R_X,         COL_Y,       COL_W,          COL_H,   '1E293B', '22C55E', 2000),

    textBox(6, 'Title', PAD, 170000, W - PAD * 2, 640000, 'ctr',
      para('Challenges Faced During Development', 2700, true, 'F97316') +
      para('Many scenarios failed in real testing — each was raised as a bug, investigated and resolved', 1150, false, '64748B')
    ),

    textBox(7, 'LHdr', L_X + INN, COL_Y + 90000, COL_W - INN * 2, 280000, 'ctr',
      para('What Went Wrong', 1400, true, 'F97316')
    ),
    solidRect(8, 'LLine', L_X + INN, COL_Y + 390000, COL_W - INN * 2, 18288, 'F97316'),

    textBox(9, 'RHdr', R_X + INN, COL_Y + 90000, COL_W - INN * 2, 280000, 'ctr',
      para('How We Raised & Fixed It', 1400, true, '22C55E')
    ),
    solidRect(10, 'RLine', R_X + INN, COL_Y + 390000, COL_W - INN * 2, 18288, '22C55E'),

    textBox(11, 'LContent', L_X + INN, COL_Y + 440000, COL_W - INN * 2, COL_H - 480000, 't', leftBullets),
    textBox(12, 'RContent', R_X + INN, COL_Y + 440000, COL_W - INN * 2, COL_H - 480000, 't', rightBullets),
  ].join('');

  return (
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
    `<p:sld ${NS}><p:cSld><p:spTree>` +
    `<p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>` +
    `<p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/>` +
    `<a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>` +
    shapes +
    `</p:spTree></p:cSld></p:sld>`
  );
}

// ─── Slide 11: BRD Verdict ────────────────────────────────────────────────────
function buildBrdSlide() {
  const W   = 12192000;
  const H   = 6858000;
  const PAD = 457200;
  const INN = 182880;
  const COL_W = 5532240;
  const COL_H = 4200000;
  const COL_Y = 1900000;
  const L_X   = PAD;
  const R_X   = W - PAD - COL_W;

  // Left: What the BRD gets right (green ticks)
  const leftBullets =
    bullet('Executive summary clearly defines the business problem and goals', 1100, '86EFAC', 650) +
    bullet('Three user roles — Patient, Staff, Admin — with distinct responsibilities', 1100, '86EFAC', 650) +
    bullet('Core features described: preferred slot swap, flexible intake, walk-in flow, conflict detection', 1100, '86EFAC', 650) +
    bullet('Out-of-scope items explicitly listed — reduces scope creep risk', 1100, '86EFAC', 650) +
    bullet('NFRs cover HIPAA compliance, RBAC, audit logging, Redis caching, and 99.9% uptime', 1100, '86EFAC', 650);

  // Right: What's missing (orange warnings)
  const rightBullets =
    bullet('No auth / registration flow defined — caused wrong patient ID linked on booking', 1100, 'FCA5A5', 650) +
    bullet('No API field contracts — led to field name mismatches between frontend and backend DTOs', 1100, 'FCA5A5', 650) +
    bullet('No slot time-window rules — past slots selectable, expired seed data blocked re-seeding', 1100, 'FCA5A5', 650) +
    bullet('No role-to-endpoint mapping — Admin got 403 on staff routes not listed in the BRD', 1100, 'FCA5A5', 650) +
    bullet('PHI column list missing — ILIKE search ran on ciphertext, encryption setup duplicated', 1100, 'FCA5A5', 650) +
    bullet('Tech stack left as "React or Angular / .NET or Java" — no decision made at BRD stage', 1100, 'FCA5A5', 650) +
    bullet('Infrastructure contradicts itself: "Netlify/Vercel" vs "Windows IIS + Redis" in same doc', 1100, 'FCA5A5', 650);

  // Verdict bar at bottom
  const verdictText =
    para('Verdict:', 1200, true, 'F97316') +
    para('The BRD is sufficient to understand the business problem — but insufficient as a development contract.  ' +
         'Adding an auth spec, API field contracts, role-endpoint matrix, PHI column list, and a firm tech-stack decision ' +
         'would have prevented most of the 13 bugs raised during development.', 1050, false, 'CBD5E1');

  const shapes = [
    solidRect(2, 'BG',      0,          0,           W,              H,       '0F172A'),
    solidRect(3, 'Accent',  PAD,        860000,      2400000,        45720,   'F97316'),
    solidRect(4, 'LCard',   L_X,        COL_Y,       COL_W,          COL_H,   '1E293B', '22C55E', 2000),
    solidRect(5, 'RCard',   R_X,        COL_Y,       COL_W,          COL_H,   '1E293B', 'EF4444', 2000),
    solidRect(6, 'VBar',    PAD,        6120000,     W - PAD * 2,    560000,  '1E3A5F'),

    textBox(7, 'Title', PAD, 170000, W - PAD * 2, 660000, 'ctr',
      para('BRD Analysis — Is It Enough to Build the Project?', 2500, true, 'F97316') +
      para('The BRD defines the vision well — but several technical gaps needed to be filled during implementation', 1100, false, '64748B')
    ),

    textBox(8, 'LHdr', L_X + INN, COL_Y + 90000, COL_W - INN * 2, 280000, 'ctr',
      para('\u2714  What the BRD Gets Right', 1350, true, '22C55E')
    ),
    solidRect(9, 'LLine', L_X + INN, COL_Y + 390000, COL_W - INN * 2, 18288, '22C55E'),

    textBox(10, 'RHdr', R_X + INN, COL_Y + 90000, COL_W - INN * 2, 280000, 'ctr',
      para('\u26A0  What Is Missing / Vague', 1350, true, 'EF4444')
    ),
    solidRect(11, 'RLine', R_X + INN, COL_Y + 390000, COL_W - INN * 2, 18288, 'EF4444'),

    textBox(12, 'LContent', L_X + INN, COL_Y + 440000, COL_W - INN * 2, COL_H - 480000, 't', leftBullets),
    textBox(13, 'RContent', R_X + INN, COL_Y + 440000, COL_W - INN * 2, COL_H - 480000, 't', rightBullets),
    textBox(14, 'Verdict',  PAD + INN, 6150000, W - (PAD + INN) * 2, 520000, 'ctr', verdictText),
  ].join('');

  return (
    `<?xml version="1.0" encoding="UTF-8" standalone="yes"?>` +
    `<p:sld ${NS}><p:cSld><p:spTree>` +
    `<p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>` +
    `<p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/>` +
    `<a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>` +
    shapes +
    `</p:spTree></p:cSld></p:sld>`
  );
}

// ─── Slide 5: patient bullets without OTP, larger font ───────────────────────
function patientBulletsXml() {
  const items = [
    'Book, reschedule or cancel appointments',
    'Fill intake form (chat-based or manual)',
    'Tag a preferred slot — auto-swapped when it opens',
    'View appointment history',
    'Receive SMS / email reminders',
  ];
  return items.map(t => bullet(t, 1250, '94A3B8', 900, 342900)).join('');
}

// ─── Main ─────────────────────────────────────────────────────────────────────
const zip = new AdmZip(PPTX_IN);

/* 1. Fix Slide 5 */
let s5 = zip.readAsText('ppt/slides/slide5.xml');
const t5idx    = s5.indexOf('"Text 5"');
const tb5open  = s5.indexOf('<p:txBody>', t5idx);
const tb5close = s5.indexOf('</p:txBody>', tb5open) + '</p:txBody>'.length;
const newTb5   =
  `<p:txBody><a:bodyPr wrap="square" rtlCol="0" anchor="ctr"/><a:lstStyle/>` +
  patientBulletsXml() +
  `</p:txBody>`;
s5 = s5.slice(0, tb5open) + newTb5 + s5.slice(tb5close);
zip.updateFile('ppt/slides/slide5.xml', Buffer.from(s5, 'utf8'));
console.log('Slide 5: OTP bullet removed, remaining bullets enlarged');

/* 2. Add challenge slide (slide10) and BRD verdict slide (slide11) */
zip.addFile('ppt/slides/slide10.xml',            Buffer.from(buildChallengeSlide(), 'utf8'));
zip.addFile('ppt/slides/_rels/slide10.xml.rels', Buffer.from(slideRels(),           'utf8'));
zip.addFile('ppt/slides/slide11.xml',            Buffer.from(buildBrdSlide(),       'utf8'));
zip.addFile('ppt/slides/_rels/slide11.xml.rels', Buffer.from(slideRels(),           'utf8'));

/* 3. Register in presentation.xml — both before Thank You (id=264 / rId12) */
let presXml = zip.readAsText('ppt/presentation.xml');
presXml = presXml.replace(
  '<p:sldId id="264" r:id="rId12"/>',
  '<p:sldId id="265" r:id="rId16"/><p:sldId id="266" r:id="rId17"/><p:sldId id="264" r:id="rId12"/>'
);
zip.updateFile('ppt/presentation.xml', Buffer.from(presXml, 'utf8'));

/* 4. Add relationships */
const SLIDE_REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide';
let presRels = zip.readAsText('ppt/_rels/presentation.xml.rels');
presRels = presRels.replace(
  '</Relationships>',
  `<Relationship Id="rId16" Type="${SLIDE_REL}" Target="slides/slide10.xml"/>` +
  `<Relationship Id="rId17" Type="${SLIDE_REL}" Target="slides/slide11.xml"/>` +
  `</Relationships>`
);
zip.updateFile('ppt/_rels/presentation.xml.rels', Buffer.from(presRels, 'utf8'));

/* 5. Register content types */
const SLIDE_CT = 'application/vnd.openxmlformats-officedocument.presentationml.slide+xml';
let ct = zip.readAsText('[Content_Types].xml');
ct = ct.replace(
  '</Types>',
  `<Override PartName="/ppt/slides/slide10.xml" ContentType="${SLIDE_CT}"/>` +
  `<Override PartName="/ppt/slides/slide11.xml" ContentType="${SLIDE_CT}"/>` +
  `</Types>`
);
zip.updateFile('[Content_Types].xml', Buffer.from(ct, 'utf8'));

/* 6. Write */
zip.writeZip(PPTX_OUT);
console.log(`\nSaved -> ${PPTX_OUT}`);
console.log('  Slide  5 : Patient section — OTP removed, bullets enlarged');
console.log('  Slide 10 : "Challenges Faced During Development"');
console.log('  Slide 11 : "BRD Analysis — Is It Enough?" (verdict slide)');
