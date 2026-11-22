namespace EventCheckIn.Server;

/// <summary>Admin UI for setting the event name, logo, questions and required documents.</summary>
public static class EventEditorPage
{
    public static string Html => Head + DashboardEndpoints.Nav("event") + Body;

    const string Script = """
    <script>
    const TYPES = {text:'Free text', radio:'Single choice (radio buttons)', checkbox:'Multiple choice (checkboxes)', dropdown:'Dropdown list'};
    const KINDS = {none:'Any text', email:'Email address', phone:'Phone number', number:'Number', regex:'Custom pattern (regex)'};
    let cfg = null, dirty = false;

    const $ = s => document.querySelector(s);
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
    const newId = p => p + Math.random().toString(36).slice(2, 8);
    const numOrNull = v => v === '' || v == null ? null : Number(v);
    function markDirty(){ dirty = true; $('#savebar').classList.add('dirty'); $('#status').textContent = 'Unsaved changes'; }
    window.addEventListener('beforeunload', e => { if (dirty) { e.preventDefault(); e.returnValue = ''; } });

    async function load(){
      cfg = await (await fetch('/admin/event')).json();
      cfg.adminRoles = cfg.adminRoles ?? [];
      $('#eventName').value = cfg.eventName; $('#welcome').value = cfg.welcomeMessage;
      $('#startButtonText').value = cfg.startButtonText || 'Start Check-In →';
      $('#terms').value = cfg.termsText; $('#photo').checked = cfg.collectPhoto;
      $('#version').textContent = 'Version ' + cfg.version;
      refreshLogo(); renderFields(); renderDocs(); renderRoles();
      dirty = false; $('#savebar').classList.remove('dirty'); $('#status').textContent = 'All changes saved';
    }

    // ---------- fields ----------
    function renderFields(){
      const host = $('#fields'); host.innerHTML = '';
      if (!cfg.fields.length) host.innerHTML = '<p><small>No extra questions. Kiosks will only ask for the attendee\'s name.</small></p>';
      cfg.fields.forEach((f, i) => {
        f.parentFieldId = f.parentFieldId ?? '';
        f.showWhenValues = f.showWhenValues ?? [];
        f.showBeforeStart = f.showBeforeStart ?? true;
        const el = document.createElement('div'); el.className = 'item' + (f.parentFieldId ? ' followup' : '');
        const isText = f.type === 'text';
        const parentCandidates = cfg.fields.filter((p, pi) => pi !== i && p.type !== 'text' && p.id !== f.id);
        const selectedParent = parentCandidates.find(p => p.id === f.parentFieldId);
        if (!selectedParent && f.parentFieldId) { f.parentFieldId = ''; f.showWhenValues = []; }
        el.innerHTML = `
          <div class=row>
            <label class=grow>Question / label<input data-k=label value="${esc(f.label)}" placeholder="e.g. Company"></label>
            <label>Type<select data-k=type>${Object.entries(TYPES).map(([k,v])=>`<option value=${k} ${k===f.type?'selected':''}>${v}</option>`).join('')}</select></label>
            <label class=chk><input type=checkbox data-k=required ${f.required?'checked':''}> Required</label>
            <label class=chk title="If checked, this question is shown on the initial screen before the attendee clicks the Start Check-In button."><input type=checkbox data-k=showBeforeStart ${f.showBeforeStart?'checked':''}> Display before Start Check-In</label>
          </div>
          ${isText ? `
          <div class=row>
            <label class=grow>Placeholder<input data-k=placeholder value="${esc(f.placeholder)}"></label>
            <label>Validation<select data-v=kind>${Object.entries(KINDS).map(([k,v])=>`<option value=${k} ${k===f.validation.kind?'selected':''}>${v}</option>`).join('')}</select></label>
            <label class=sm>Min length<input type=number min=0 data-v=minLength value="${f.validation.minLength ?? ''}"></label>
            <label class=sm>Max length<input type=number min=1 data-v=maxLength value="${f.validation.maxLength ?? ''}"></label>
          </div>
          <div class=row>
            ${f.validation.kind==='number' ? `
              <label class=sm>Minimum<input type=number step=any data-v=min value="${f.validation.min ?? ''}"></label>
              <label class=sm>Maximum<input type=number step=any data-v=max value="${f.validation.max ?? ''}"></label>` : ''}
            ${f.validation.kind==='regex' ? `
              <label class=grow>Pattern (whole value must match)<input data-v=pattern value="${esc(f.validation.pattern)}" placeholder="e.g. [A-Z]{2}[0-9]{4}"></label>
              <label class=grow>Test value<input data-test placeholder="type to test"><small data-testout></small></label>` : ''}
            <label class=grow>Error message (optional)<input data-v=message value="${esc(f.validation.message)}" placeholder="Shown when the value isn't valid"></label>
          </div>` : `
          <div class=row><label class=grow>Options (one per line)
            <textarea data-k=options rows=4 placeholder="Option A&#10;Option B">${esc(f.options.join('\n'))}</textarea></label>
            <div class=preview><small>Preview</small>${preview(f)}</div></div>`}
          <div class="row condrow">
            <label>Show this question
              <select data-k=parentFieldId>
                <option value="" ${!f.parentFieldId?'selected':''}>Always</option>
                ${parentCandidates.map(p => `<option value="${esc(p.id)}" ${p.id===f.parentFieldId?'selected':''}>Only when "${esc(p.label || '(unnamed question)')}" is…</option>`).join('')}
              </select>
            </label>
            ${selectedParent ? `
              <div class=grow>
                <div style="font-size:13px;color:#555;margin-bottom:4px">Trigger when any of these answers are chosen:</div>
                <div class=condopts>
                  ${(selectedParent.options.length ? selectedParent.options : []).map(opt => `
                    <label class=chk><input type=checkbox data-when="${esc(opt)}" ${f.showWhenValues.includes(opt)?'checked':''}> ${esc(opt)}</label>
                  `).join('') || '<small>Add options to the parent question first.</small>'}
                </div>
              </div>
            ` : ''}
          </div>
          <div class=actions>
            ${!isText ? `<button class=secondary data-act=followup title="Add a follow-up question that depends on this question's answer">+ Add follow-up question</button>` : ''}
            <button class=secondary data-act=up ${i===0?'disabled':''}>↑</button>
            <button class=secondary data-act=down ${i===cfg.fields.length-1?'disabled':''}>↓</button>
            <button class=danger data-act=del>Remove</button>
          </div>`;
        el.addEventListener('input', e => onFieldInput(f, e, el));
        el.addEventListener('change', e => {
          if (e.target.dataset.k === 'type' || e.target.dataset.v === 'kind' || e.target.dataset.k === 'parentFieldId' || e.target.dataset.k === 'options') renderFields();
        });
        el.querySelectorAll('[data-act]').forEach(b => b.onclick = () => {
          if (b.dataset.act === 'followup') addFollowUp(f, i);
          else move(cfg.fields, i, b.dataset.act, renderFields);
        });
        host.appendChild(el);
      });
    }

    function preview(f){
      const opts = f.options.length ? f.options : ['(add options)'];
      if (f.type === 'dropdown') return `<select>${opts.map(o=>`<option>${esc(o)}</option>`).join('')}</select>`;
      const t = f.type === 'radio' ? 'radio' : 'checkbox';
      return opts.map(o => `<label class=chk><input type=${t} name=p${esc(f.id)}> ${esc(o)}</label>`).join('<br>');
    }

    function onFieldInput(f, e, el){
      const t = e.target;
      if (t.dataset.when !== undefined){
        const val = t.dataset.when;
        f.showWhenValues = f.showWhenValues ?? [];
        if (t.checked && !f.showWhenValues.includes(val)) f.showWhenValues.push(val);
        else if (!t.checked) f.showWhenValues = f.showWhenValues.filter(v => v !== val);
        markDirty();
        return;
      }
      if (t.dataset.k){
        const k = t.dataset.k;
        if (k === 'required') f.required = t.checked;
        else if (k === 'showBeforeStart') f.showBeforeStart = t.checked;
        else if (k === 'options'){
          f.options = t.value.split('\n').map(s=>s.trim()).filter(Boolean);
          el.querySelector('.preview').innerHTML = '<small>Preview</small>' + preview(f);
          cfg.fields.forEach(c => { if (c.parentFieldId === f.id) c.showWhenValues = (c.showWhenValues || []).filter(v => f.options.includes(v)); });
        }
        else if (k === 'type'){
          f.type = t.value;
          if (f.type !== 'text' && !f.options.length) f.options = ['Option 1','Option 2'];
          if (f.type === 'text') cfg.fields.forEach(c => { if (c.parentFieldId === f.id) { c.parentFieldId = ''; c.showWhenValues = []; } });
        }
        else if (k === 'parentFieldId'){
          f.parentFieldId = t.value;
          const p = cfg.fields.find(x => x.id === f.parentFieldId);
          f.showWhenValues = p && p.options.length ? [p.options[0]] : [];
        }
        else f[k] = t.value;
      } else if (t.dataset.v){
        const k = t.dataset.v;
        f.validation[k] = ['minLength','maxLength','min','max'].includes(k) ? numOrNull(t.value) : t.value;
      }
      if (t.dataset.test !== undefined || t.dataset.v === 'pattern'){
        const out = el.querySelector('[data-testout]'), test = el.querySelector('[data-test]');
        if (out && test){
          try { const ok = new RegExp('^(?:' + f.validation.pattern + ')$').test(test.value);
                out.textContent = test.value ? (ok ? ' ✅ matches' : ' ❌ no match') : ''; }
          catch { out.textContent = ' ⚠ invalid pattern'; }
        }
      }
      markDirty();
    }

    function addField(){
      cfg.fields.push({id:newId('f'), label:'', type:'text', required:false, showBeforeStart:true, placeholder:'', options:[],
                       parentFieldId:'', showWhenValues:[],
                       validation:{kind:'none', pattern:'', minLength:null, maxLength:null, min:null, max:null, message:''}});
      renderFields(); markDirty();
      document.querySelectorAll('#fields [data-k=label]').forEach((x,i,a)=>{ if(i===a.length-1) x.focus(); });
    }

    function addFollowUp(parentField, parentIndex){
      const child = {id:newId('f'), label:'', type:'text', required:true, showBeforeStart:parentField.showBeforeStart ?? true, placeholder:'', options:[],
                     parentFieldId:parentField.id, showWhenValues:parentField.options.length ? [parentField.options[0]] : [],
                     validation:{kind:'none', pattern:'', minLength:null, maxLength:null, min:null, max:null, message:''}};
      cfg.fields.splice(parentIndex + 1, 0, child);
      renderFields(); markDirty();
      const labels = document.querySelectorAll('#fields [data-k=label]');
      if (labels[parentIndex + 1]) labels[parentIndex + 1].focus();
    }

    // ---------- documents ----------
    function renderDocs(){
      const host = $('#docs'); host.innerHTML = '';
      if (!cfg.documents.length) host.innerHTML = '<p><small>No documents requested. Kiosks will skip the scanning step.</small></p>';
      cfg.documents.forEach((d, i) => {
        d.requirePinToView = d.requirePinToView ?? false;
        const el = document.createElement('div'); el.className = 'item';
        el.innerHTML = `
          <div class=row>
            <label class=grow>Document name<input data-k=name value="${esc(d.name)}" placeholder="e.g. Proof of insurance"></label>
            <label class=grow>Instructions shown on the kiosk<input data-k=instructions value="${esc(d.instructions)}" placeholder="e.g. Front side, hold flat"></label>
            <label class=chk><input type=checkbox data-k=required ${d.required?'checked':''}> Required</label>
            <label class=chk title="When checked, admins with permission to view this document must also enter their role's PIN/password to unlock and view it."><input type=checkbox data-k=requirePinToView ${d.requirePinToView?'checked':''}> 🔒 Require PIN to view</label>
          </div>
          <div class=actions>
            <button class=secondary data-act=up ${i===0?'disabled':''}>↑</button>
            <button class=secondary data-act=down ${i===cfg.documents.length-1?'disabled':''}>↓</button>
            <button class=danger data-act=del>Remove</button>
          </div>`;
        el.addEventListener('input', e => { const k = e.target.dataset.k; if (!k) return;
          d[k] = (k === 'required' || k === 'requirePinToView') ? e.target.checked : e.target.value;
          if (k === 'name' || k === 'requirePinToView') renderRoles();
          markDirty(); });
        el.querySelectorAll('[data-act]').forEach(b => b.onclick = () => move(cfg.documents, i, b.dataset.act, () => { renderDocs(); renderRoles(); }));
        host.appendChild(el);
      });
    }
    function addDoc(){
      const d = {id:newId('d'), name:'', instructions:'', required:true, requirePinToView:false};
      cfg.documents.push(d);
      renderDocs(); renderRoles(); markDirty();
    }

    // ---------- admin roles & document permissions ----------
    function renderRoles(){
      const host = $('#roles'); if (!host) return; host.innerHTML = '';
      cfg.adminRoles = cfg.adminRoles ?? [];
      if (!cfg.adminRoles.length) host.innerHTML = '<p><small>No admin roles defined. Add at least one role to control which documents admin tablets can view.</small></p>';
      cfg.adminRoles.forEach((r, i) => {
        r.allowedDocumentIds = r.allowedDocumentIds ?? [];
        const el = document.createElement('div'); el.className = 'item';
        const pinStatus = r.clearPin ? 'PIN will be cleared on save' : (r.newPin ? 'New PIN entered (save to apply)' : (r.hasPin ? '🔒 PIN is set' : 'No PIN set'));
        el.innerHTML = `
          <div class=row>
            <label class=grow>Role name<input data-rk=name value="${esc(r.name)}" placeholder="e.g. Front Desk Lead, Security / HR"></label>
            <label class=grow>Role PIN / Password (${pinStatus})
              <input type=password data-rk=newPin value="${esc(r.newPin ?? '')}" placeholder="${r.hasPin && !r.clearPin ? 'Leave blank to keep existing PIN' : 'Enter PIN/password for 🔒 documents'}">
            </label>
            ${r.hasPin && !r.clearPin ? `<button type=button class=secondary data-ract=clearpin style="margin-bottom:2px">Clear PIN</button>` : ''}
          </div>
          <div class="row condrow">
            <div class=grow>
              <div style="font-size:13px;color:#555;margin-bottom:4px"><b>Permitted documents for this role:</b></div>
              <div class=condopts>
                ${cfg.documents.length ? cfg.documents.map(d => `
                  <label class=chk>
                    <input type=checkbox data-rdoc="${esc(d.id)}" ${r.allowedDocumentIds.includes(d.id)?'checked':''}>
                    ${esc(d.name || '(unnamed document)')}${d.requirePinToView ? ' 🔒' : ''}
                  </label>
                `).join('') : '<small>No documents configured in "Documents to scan" above.</small>'}
              </div>
            </div>
          </div>
          <div class=actions>
            <button class=secondary data-ract=up ${i===0?'disabled':''}>↑</button>
            <button class=secondary data-ract=down ${i===cfg.adminRoles.length-1?'disabled':''}>↓</button>
            <button class=danger data-ract=del ${cfg.adminRoles.length<=1?'disabled':''}>Remove</button>
          </div>`;
        el.addEventListener('input', e => {
          const t = e.target;
          if (t.dataset.rk === 'name') { r.name = t.value; markDirty(); }
          else if (t.dataset.rk === 'newPin') { r.newPin = t.value; if (t.value) r.clearPin = false; markDirty(); }
          else if (t.dataset.rdoc !== undefined) {
            const docId = t.dataset.rdoc;
            if (t.checked && !r.allowedDocumentIds.includes(docId)) r.allowedDocumentIds.push(docId);
            else if (!t.checked) r.allowedDocumentIds = r.allowedDocumentIds.filter(x => x !== docId);
            markDirty();
          }
        });
        el.querySelectorAll('[data-ract]').forEach(b => b.onclick = () => {
          if (b.dataset.ract === 'clearpin') { r.clearPin = true; r.newPin = ''; renderRoles(); markDirty(); }
          else move(cfg.adminRoles, i, b.dataset.ract, renderRoles);
        });
        host.appendChild(el);
      });
    }
    function addRole(){
      cfg.adminRoles.push({id:newId('role_'), name:'', allowedDocumentIds:cfg.documents.map(d=>d.id), hasPin:false, newPin:'', clearPin:false});
      renderRoles(); markDirty();
    }

    function move(list, i, act, rerender){
      if (act === 'del'){ if (!confirm('Remove this item?')) return; list.splice(i,1); }
      else { const j = act === 'up' ? i-1 : i+1; [list[i], list[j]] = [list[j], list[i]]; }
      rerender(); markDirty();
    }

    // ---------- logo ----------
    function refreshLogo(){
      $('#logoPreview').innerHTML = cfg.hasLogo ? `<img src="/admin/event/logo?v=${Date.now()}">` : '<div class=nologo>No logo yet</div>';
      $('#logoDelete').style.display = cfg.hasLogo ? '' : 'none';
    }
    async function uploadLogo(input){
      if (!input.files.length) return;
      const fd = new FormData(); fd.append('logo', input.files[0]);
      const r = await fetch('/admin/event/logo', {method:'POST', body:fd});
      const j = await r.json(); input.value = '';
      if (!r.ok) return alert(j.error || 'Upload failed');
      cfg.hasLogo = true; refreshLogo(); flash('Logo updated – kiosks will show it shortly.');
      const fresh = await (await fetch('/admin/event')).json(); cfg.version = fresh.version; $('#version').textContent = 'Version ' + cfg.version;
    }
    async function deleteLogo(){
      if (!confirm('Remove the logo?')) return;
      await fetch('/admin/event/logo/delete', {method:'POST'});
      cfg.hasLogo = false; refreshLogo(); flash('Logo removed.');
    }

    // ---------- save ----------
    async function save(){
      cfg.eventName = $('#eventName').value; cfg.welcomeMessage = $('#welcome').value;
      cfg.startButtonText = $('#startButtonText').value || 'Start Check-In →';
      cfg.termsText = $('#terms').value; cfg.collectPhoto = $('#photo').checked;
      const r = await fetch('/admin/event', {method:'POST', headers:{'content-type':'application/json'}, body:JSON.stringify(cfg)});
      const j = await r.json();
      if (!r.ok){ $('#errors').innerHTML = '<b>Please fix:</b><ul>' + j.errors.map(e=>`<li>${esc(e)}</li>`).join('') + '</ul>'; $('#errors').style.display='block'; window.scrollTo(0,0); return; }
      $('#errors').style.display = 'none';
      await load(); flash('Saved. Kiosks will update within about 20 seconds.');
    }
    function flash(msg){ const s = $('#status'); s.textContent = msg; }

    ['#eventName','#welcome','#startButtonText','#terms','#photo'].forEach(s => document.addEventListener('DOMContentLoaded', () => $(s).addEventListener('input', markDirty)));
    document.addEventListener('DOMContentLoaded', load);
    </script>
    """;

    const string Head = """
    <!doctype html><html><head><meta charset=utf-8><title>Event setup – EventCheckIn Host</title>
    <style>
    """ + DashboardEndpoints.SharedCss + """
      main{max-width:980px}
      section{background:#fff;border-radius:12px;padding:18px 22px;box-shadow:0 1px 4px #0002;margin-bottom:18px}
      h2{margin:0 0 12px;font-size:18px}
      label{display:flex;flex-direction:column;font-size:13px;color:#555;gap:4px}
      label.chk{flex-direction:row;align-items:center;gap:6px;color:#222;font-size:14px}
      input,select,textarea{font:inherit;font-size:14px;padding:7px 9px;border:1px solid #ccc;border-radius:6px}
      input[type=checkbox],input[type=radio]{padding:0}
      .row{display:flex;gap:12px;flex-wrap:wrap;align-items:flex-end;margin-bottom:10px}
      .grow{flex:1;min-width:200px} .sm{width:110px}
      .item{border:1px solid #e3e3ea;border-radius:10px;padding:12px 14px;margin-bottom:12px;background:#fbfbfe}
      .item.followup{margin-left:28px;border-left:4px solid #512BD4;background:#f7f5ff}
      .condrow{background:#f1effa;padding:8px 10px;border-radius:8px;align-items:flex-start}
      .condopts{display:flex;gap:14px;flex-wrap:wrap;padding:4px 0}
      .actions{display:flex;gap:6px;justify-content:flex-end}
      .actions button{padding:4px 10px}
      .preview{border-left:3px solid #512BD4;padding-left:12px;min-width:200px}
      #logoPreview img{max-height:90px;max-width:260px;border:1px solid #eee;border-radius:6px}
      .nologo{width:200px;height:70px;border:2px dashed #ccc;border-radius:8px;display:flex;align-items:center;justify-content:center;color:#999;font-size:13px}
      #savebar{position:sticky;bottom:0;background:#fff;border-top:1px solid #ddd;padding:12px 24px;display:flex;gap:12px;align-items:center}
      #savebar.dirty{background:#fff8e1}
      #errors{display:none;background:#fdecea;border:1px solid #f5c2c0;color:#8a1c1c;border-radius:10px;padding:12px 18px;margin-bottom:18px}
    </style></head><body>
    """;

    const string Body = """
    <main>
      <div id=errors></div>
      <section>
        <h2>Event</h2>
        <div class=row>
          <label class=grow>Event name<input id=eventName maxlength=120></label>
          <span id=version style="color:#888;font-size:13px"></span>
        </div>
        <div class=row>
          <label class=grow>Welcome message (shown above the form)<input id=welcome maxlength=500></label>
          <label class=grow>Start check-in button text<input id=startButtonText maxlength=80 placeholder="Start Check-In →"></label>
        </div>
        <div class=row>
          <div class=grow><div style="font-size:13px;color:#555;margin-bottom:4px"><b>Logo</b> – shown at the top of every kiosk (PNG or JPEG, max 5 MB)</div>
            <div id=logoPreview></div></div>
          <input type=file id=logoFile accept="image/png,image/jpeg" style="display:none" onchange="uploadLogo(this)">
          <button type=button onclick="document.getElementById('logoFile').click()">🖼 Upload logo…</button>
          <button type=button class=danger id=logoDelete onclick="deleteLogo()">Remove logo</button>
        </div>
      </section>

      <section>
        <h2>Information to collect</h2>
        <p><small>The attendee's <b>full name</b> is always collected. Add any other questions below.</small></p>
        <div id=fields></div>
        <button onclick="addField()">+ Add question</button>
      </section>

      <section>
        <h2>Documents to scan</h2>
        <p><small>The kiosk asks for each document in order. Optional documents can be skipped. Check <b>🔒 Require PIN to view</b> on sensitive documents to require an admin's role PIN before viewing on an Admin tablet.</small></p>
        <div id=docs></div>
        <button onclick="addDoc()">+ Add document</button>
      </section>

      <section>
        <h2>Admin roles &amp; document permissions</h2>
        <p><small>Define access levels for Admin tablets. Assign each paired Admin tablet to a role on the <b>Check-ins</b> dashboard. An admin tablet can only view documents checked for its role, and must enter the role's PIN/password for documents marked <b>🔒 Require PIN to view</b>.</small></p>
        <div id=roles></div>
        <button onclick="addRole()">+ Add admin role</button>
      </section>

      <section>
        <h2>Photo &amp; signature</h2>
        <div class=row><label class=chk><input type=checkbox id=photo> Take the attendee's photo</label></div>
        <div class=row><label class=grow>Agreement text shown above the signature<textarea id=terms rows=3 maxlength=2000></textarea></label></div>
      </section>
    </main>
    <div id=savebar><button onclick="save()">Save &amp; publish to kiosks</button>
      <button class=secondary onclick="if(!dirty||confirm('Discard changes?'))load()">Discard changes</button>
      <span id=status></span></div>
    """ + Script + "</body></html>";
}
