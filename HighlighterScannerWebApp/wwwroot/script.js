const API_BASE_URL = window.location.origin + '/api'; 

let token = localStorage.getItem('authToken'); // changed authToken to token cause it's shorter

function getHeaders() {
    return { 'Authorization': `Bearer ${token}` };
}

function toggleAuth(showLogin) {
    // reset error if we switch
    showAuthError(null);
    document.getElementById('login-form').classList.toggle('hidden', !showLogin);
    document.getElementById('register-form').classList.toggle('hidden', showLogin);
}

async function register() {
    const e = document.getElementById('regEmail').value;
    const p = document.getElementById('regPassword').value;
    
    if (!e || !p) {
        showAuthError("hey, fill in both fields!");
        return;
    }

    try {
        console.log("trying to register...");
        const res = await fetch(`${API_BASE_URL}/auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: e, password: p })
        });
        
        if (res.ok) {
            showAuthError(null);
            alert("Registration worked! Now log in.");
            toggleAuth(true);
        } else {
            const txt = await res.text();
            showAuthError(txt || "Register failed :(");
        }
    } catch (err) {
        showAuthError("network is being weird.");
    }
}

async function login() {
    const email = document.getElementById('loginEmail').value;
    const pass = document.getElementById('loginPassword').value;

    if (!email || !pass) {
        showAuthError("email and password please!");
        return;
    }

    try {
        console.log("logging in user...");
        const response = await fetch(`${API_BASE_URL}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: email, password: pass })
        });

        if (response.ok) {
            const data = await response.json();
            token = data.token;
            localStorage.setItem('authToken', token);
            localStorage.setItem('userEmail', email);
            showAuthError(null);
            updateUI();
            loadColours();
            loadTimeline();
        } else {
            const msg = await response.text();
            showAuthError(msg || "login failed... check details");
        }
    } catch (e) {
        showAuthError("can't reach the server.");
    }
}

function logout() {
    if (confirm("ready to leave?")) {
        token = null;
        localStorage.removeItem('authToken');
        localStorage.removeItem('userEmail');
        updateUI();
    }
}

function showAuthError(m) {
    const div = document.getElementById('auth-error');
    if (!div) return;
    if (m) {
        div.innerText = m;
        div.classList.remove('hidden');
    } else {
        div.classList.add('hidden');
    }
}

function updateUI() {
    const logged = !!token;
    const email = localStorage.getItem('userEmail');
    
    document.getElementById('login-form').classList.add('hidden');
    document.getElementById('register-form').classList.add('hidden');
    document.getElementById('logged-in-info').classList.toggle('hidden', !logged);
    document.getElementById('main-content').classList.toggle('hidden', !logged);
    
    if (!logged) {
        toggleAuth(true);
    } else {
        const name = email ? email.split('@')[0] : 'Guest';
        const cap = name.charAt(0).toUpperCase() + name.slice(1);
        document.getElementById('user-display').innerText = `${cap} (${email})`;
    }
}

async function authFetch(url, opts = {}) {
    opts.headers = { 
        ...opts.headers, 
        'Authorization': `Bearer ${token}` 
    };
    
    try {
        const r = await fetch(url, opts);
        if (r.status === 401) {
            alert("oops, your session died. log in again.");
            token = null;
            localStorage.removeItem('authToken');
            updateUI();
            return null;
        }
        return r;
    } catch (err) {
        console.error("fetch failed big time:", err);
        throw err;
    }
}

async function loadColours() {
    if (!token) return;
    try {
        const res = await authFetch(`${API_BASE_URL}/scanner/colours`);
        if (!res) return;
        if (!res.ok) throw new Error("bad response for colours");
        
        const data = await res.json();
        const sel = document.getElementById('colourSelect');
        sel.innerHTML = '<option value="">-- Choose a colour --</option>';
        
        data.forEach(c => {
            const o = document.createElement('option');
            o.value = c.id;
            o.textContent = c.colourName;
            sel.appendChild(o);
        });
    } catch (err) {
        console.error("colours failed:", err);
    }
}

document.getElementById('colourForm').addEventListener('submit', async (ev) => {
    ev.preventDefault();
    
    const obj = {
        colourName: document.getElementById('colourName').value,
        hue: parseInt(document.getElementById('hue').value),
        saturation: parseInt(document.getElementById('saturation').value),
        value: parseInt(document.getElementById('value').value)
    };

    try {
        console.log("saving new colour profile...");
        const res = await authFetch(`${API_BASE_URL}/scanner/colours`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(obj)
        });

        if (res && res.ok) {
            document.getElementById('colourForm').reset();
            document.getElementById('hueVal').innerText = "60";
            document.getElementById('saturationVal').innerText = "100";
            document.getElementById('valueVal').innerText = "100";
            updatePreview();
            document.getElementById('colour-management-card').classList.add('hidden');
            loadColours();
        }
    } catch (e) {
        alert("couldn't save the colour profile :/");
    }
});

document.getElementById('uploadForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    
    const id = document.getElementById('colourSelect').value;
    const input = document.getElementById('imageFile');
    
    if (!id) { alert("pick a colour!"); return; }
    if (input.files.length === 0) { alert("where is the file?"); return; }

    const fd = new FormData();
    fd.append('file', input.files[0]);

    try {
        console.log("scanning page...");
        const res = await authFetch(`${API_BASE_URL}/scanner/scan/${id}`, {
            method: 'POST',
            body: fd
        });

        if (res && res.ok) {
            loadTimeline();
        } else if (res) {
            const errTxt = await res.text();
            alert("scan failed: " + errTxt);
        }
    } catch (err) {
        alert("something went wrong during the scan.");
    }
});

async function loadTimeline() {
    if (!token) return;
    try {
        console.log("fetching timeline data...");
        const res = await authFetch(`${API_BASE_URL}/scanner/timeline`);
        if (!res) return;
        if (!res.ok) throw new Error("timeline failed");
        
        const data = await res.json();
        const box = document.getElementById('timeline-container');
        
        if (data.timelineEvents.length === 0) {
            box.innerHTML = '<p>nothing here yet...</p>';
            return;
        }

        const grouped = {};
        data.timelineEvents.forEach(ev => {
            const date = new Date(ev.scannedTime).toLocaleDateString();
            if (!grouped[date]) grouped[date] = [];
            grouped[date].push(ev);
        });

        const dates = Object.keys(grouped).sort((a,b) => new Date(b) - new Date(a));
        
        box.innerHTML = dates.map(d => `
            <div class="timeline-group">
                <div class="timeline-group-header" onclick="toggleGroupHeader(this)">
                    <span class="toggle-icon">▼</span>
                    <strong>${d} (${grouped[d].length} pages)</strong>
                </div>
                <div class="timeline-group-content">
                    ${grouped[d].map(ev => `
                        <div class="timeline-event">
                            <h4 style="margin:0;">Page ${ev.pageNumber}: ${ev.fileName}</h4>
                            <small class="scanned-time-colour">at ${new Date(ev.scannedTime).toLocaleTimeString()}</small>
                            <div class="extracted-text-list">
                                ${ev.extractedLines.length > 0 
                                    ? ev.extractedLines.map(l => `<div>👉 "${l.lineText}"</div>`).join('') 
                                    : '<em>no text found.</em>'}
                            </div>
                        </div>
                    `).join('')}
                </div>
            </div>
        `).join('');

    } catch (err) {
        console.error("timeline error:", err);
    }
}

function toggleGroupHeader(header) {
    const content = header.nextElementSibling;
    const icon = header.querySelector('.toggle-icon');
    const isHidden = content.classList.toggle('hidden');
    icon.innerText = isHidden ? '▶' : '▼';
}

function updatePreview() {
    const h = document.getElementById('hue').value;
    const s = document.getElementById('saturation').value;
    const v = document.getElementById('value').value;
    
    // hsv to hsl... math is hard
    const l = (v / 100) * (1 - (s / 200));
    const sL = (l === 0 || l === 1) ? 0 : (v/100 - l) / Math.min(l, 1 - l);
    
    const box = document.getElementById('colourPreview');
    if (box) {
        box.style.backgroundColor = `hsl(${h}, ${sL * 100}%, ${l * 100}%)`;
    }
}

// listeners for the sliders
['hue', 'saturation', 'value'].forEach(id => {
    const el = document.getElementById(id);
    if (el) {
        el.addEventListener('input', () => {
            document.getElementById(`${id}Val`).innerText = el.value;
            updatePreview();
        });
    }
});

// start the app
updateUI();
if (token) {
    loadColours();
    loadTimeline();
}
updatePreview();
