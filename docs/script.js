import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';

const RGBShiftShader = {
    uniforms: { "tDiffuse": { value: null }, "amount": { value: 0.0015 }, "angle": { value: 0.0 } },
    vertexShader: `varying vec2 vUv; void main() { vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4( position, 1.0 ); }`,
    fragmentShader: `uniform sampler2D tDiffuse; uniform float amount; uniform float angle; varying vec2 vUv; void main() { vec2 offset = amount * vec2( cos(angle), sin(angle)); vec4 cr = texture2D(tDiffuse, vUv + offset); vec4 cga = texture2D(tDiffuse, vUv); vec4 cb = texture2D(tDiffuse, vUv - offset); gl_FragColor = vec4(cr.r, cga.g, cb.b, cga.a); }`
};

document.addEventListener("DOMContentLoaded", () => {
    
    // Check if mobile
    const isMobile = window.innerWidth <= 768;

    // --- 0. Enter Gate & Web Audio API ---
    const enterGate = document.getElementById('enter-gate');
    const btnEnter = document.getElementById('btn-enter');
    const mainContent = document.getElementById('main-content');
    const hud = document.getElementById('live-stats-hud');
    
    let audioCtx, masterGain, droneOsc;

    function initAudio() {
        audioCtx = new (window.AudioContext || window.webkitAudioContext)();
        masterGain = audioCtx.createGain();
        masterGain.connect(audioCtx.destination);
        masterGain.gain.value = 0.1;

        droneOsc = audioCtx.createOscillator();
        droneOsc.type = 'sawtooth';
        droneOsc.frequency.setValueAtTime(45, audioCtx.currentTime);
        
        const filter = audioCtx.createBiquadFilter();
        filter.type = 'lowpass';
        filter.frequency.setValueAtTime(200, audioCtx.currentTime);
        
        const lfo = audioCtx.createOscillator();
        lfo.type = 'sine';
        lfo.frequency.value = 0.5;
        const lfoGain = audioCtx.createGain();
        lfoGain.gain.value = 100;
        lfo.connect(lfoGain);
        lfoGain.connect(filter.frequency);
        
        droneOsc.connect(filter);
        filter.connect(masterGain);
        droneOsc.start();
        lfo.start();
    }

    function playHoverSound() {
        if(!audioCtx) return;
        const osc = audioCtx.createOscillator();
        const gain = audioCtx.createGain();
        osc.type = 'sine';
        osc.frequency.setValueAtTime(800, audioCtx.currentTime);
        osc.frequency.exponentialRampToValueAtTime(1200, audioCtx.currentTime + 0.1);
        gain.gain.setValueAtTime(0, audioCtx.currentTime);
        gain.gain.linearRampToValueAtTime(0.05, audioCtx.currentTime + 0.05);
        gain.gain.linearRampToValueAtTime(0, audioCtx.currentTime + 0.2);
        osc.connect(gain);
        gain.connect(masterGain);
        osc.start();
        osc.stop(audioCtx.currentTime + 0.2);
    }

    function playFlareSound() {
        if(!audioCtx) return;
        const osc = audioCtx.createOscillator();
        const gain = audioCtx.createGain();
        osc.type = 'square';
        osc.frequency.setValueAtTime(150, audioCtx.currentTime);
        osc.frequency.exponentialRampToValueAtTime(40, audioCtx.currentTime + 0.5);
        gain.gain.setValueAtTime(0, audioCtx.currentTime);
        gain.gain.linearRampToValueAtTime(0.2, audioCtx.currentTime + 0.05);
        gain.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + 0.5);
        osc.connect(gain);
        gain.connect(masterGain);
        osc.start();
        osc.stop(audioCtx.currentTime + 0.5);
    }

    function playTypeSound() {
        if(!audioCtx) return;
        const osc = audioCtx.createOscillator();
        const gain = audioCtx.createGain();
        osc.type = 'triangle';
        osc.frequency.setValueAtTime(2000 + Math.random()*500, audioCtx.currentTime);
        gain.gain.setValueAtTime(0.05, audioCtx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + 0.05);
        osc.connect(gain);
        gain.connect(masterGain);
        osc.start();
        osc.stop(audioCtx.currentTime + 0.05);
    }

    btnEnter.addEventListener('click', () => {
        initAudio();
        gsap.to(enterGate, {
            opacity: 0, duration: 1, onComplete: () => {
                enterGate.style.display = 'none';
                mainContent.style.display = 'block';
                hud.style.display = 'flex';
                gsap.to(mainContent, { opacity: 1, duration: 1 });
                initGSAP();
                startStatsEngine();
            }
        });
    });

    if (!isMobile) {
        document.querySelectorAll('.audio-hover').forEach(el => el.addEventListener('mouseenter', playHoverSound));
    }

    // --- 1. Custom Cursor & Touch Tracker ---
    const cursorDot = document.querySelector('.cursor-dot');
    const cursorGlow = document.querySelector('.cursor-glow');
    let mouseX = window.innerWidth / 2, mouseY = window.innerHeight / 2;
    let lastMouseX = mouseX, lastMouseY = mouseY, mouseSpeed = 0;

    window.addEventListener('mousemove', (e) => {
        mouseX = e.clientX; mouseY = e.clientY;
        if (!isMobile) {
            cursorDot.style.left = `${mouseX}px`; cursorDot.style.top = `${mouseY}px`;
            cursorGlow.animate({ left: `${mouseX}px`, top: `${mouseY}px` }, { duration: 200, fill: "forwards" });
        }
        mouseSpeed = Math.sqrt((mouseX-lastMouseX)**2 + (mouseY-lastMouseY)**2);
        lastMouseX = mouseX; lastMouseY = mouseY;
    });

    window.addEventListener('touchmove', (e) => {
        if(e.touches.length > 0) {
            mouseX = e.touches[0].clientX;
            mouseY = e.touches[0].clientY;
            mouseSpeed = Math.sqrt((mouseX-lastMouseX)**2 + (mouseY-lastMouseY)**2);
            lastMouseX = mouseX; lastMouseY = mouseY;
        }
    });

    if (!isMobile) {
        document.querySelectorAll('a, button, .magnetic, input').forEach(el => {
            el.addEventListener('mouseenter', () => document.body.classList.add('cursor-hover'));
            el.addEventListener('mouseleave', () => document.body.classList.remove('cursor-hover'));
        });
    }

    // --- 2. Magnetic UX ---
    if (!isMobile) {
        document.querySelectorAll('.magnetic').forEach(el => {
            el.addEventListener('mousemove', (e) => {
                const rect = el.getBoundingClientRect();
                const strength = el.getAttribute('data-strength') || 20;
                gsap.to(el, { x: (e.clientX - (rect.left + rect.width/2)) * (strength/100), y: (e.clientY - (rect.top + rect.height/2)) * (strength/100), duration: 0.3, ease: "power2.out" });
            });
            el.addEventListener('mouseleave', () => gsap.to(el, { x: 0, y: 0, duration: 0.7, ease: "elastic.out(1, 0.3)" }));
        });
    }

    // --- 3. Text Splitter ---
    document.querySelectorAll('.split-text').forEach(el => {
        const text = el.innerText; el.innerHTML = '';
        text.split(' ').forEach(word => {
            const wordSpan = document.createElement('span'); wordSpan.className = 'word'; wordSpan.style.display = 'inline-block'; wordSpan.style.marginRight = '0.25em';
            word.split('').forEach(char => {
                const charSpan = document.createElement('span'); charSpan.className = 'char'; charSpan.style.display = 'inline-block'; charSpan.innerText = char;
                wordSpan.appendChild(charSpan);
            });
            el.appendChild(wordSpan);
        });
    });

    // --- 4. Interactive Simulator Logic ---
    const simMap = document.getElementById('simulator-map');
    let flaresCount = 42;
    const statFlares = document.getElementById('stat-flares');

    function triggerFlare(e) {
        e.preventDefault();
        const rect = simMap.getBoundingClientRect();
        
        let clientX = e.clientX;
        let clientY = e.clientY;
        
        if(e.type === 'touchstart') {
            clientX = e.touches[0].clientX;
            clientY = e.touches[0].clientY;
        }

        const x = clientX - rect.left;
        const y = clientY - rect.top;

        const flare = document.createElement('div');
        flare.className = 'sim-flare';
        flare.style.left = `${x}px`;
        flare.style.top = `${y}px`;
        simMap.appendChild(flare);

        playFlareSound();

        flaresCount++;
        statFlares.innerText = flaresCount.toString().padStart(3, '0');
        statFlares.style.color = '#fff';
        setTimeout(() => statFlares.style.color = 'var(--neon-blue)', 200);

        setTimeout(() => flare.remove(), 2000);
    }

    simMap.addEventListener('click', triggerFlare);
    simMap.addEventListener('touchstart', triggerFlare, {passive: false});

    // --- 5. Live Stats Engine ---
    function startStatsEngine() {
        const statUsers = document.getElementById('stat-users');
        const statVenues = document.getElementById('stat-venues');
        let users = 8409;
        let venues = 115;

        setInterval(() => {
            if(Math.random() > 0.5) {
                users += Math.floor(Math.random() * 5);
                statUsers.innerText = users.toLocaleString();
            }
            if(Math.random() > 0.9) {
                venues += 1;
                statVenues.innerText = venues;
            }
            if(Math.random() > 0.7) {
                flaresCount += Math.floor(Math.random() * 2);
                statFlares.innerText = flaresCount.toString().padStart(3, '0');
            }
        }, 2000);
    }

    // --- 6. Terminal Waitlist Logic ---
    const terminal = document.getElementById('waitlist-terminal');
    const terminalOutput = document.getElementById('terminal-output');
    const terminalInputWrapper = document.getElementById('terminal-input-wrapper');
    const terminalInput = document.getElementById('terminal-input');
    let isB2B = false;

    document.querySelectorAll('.cta-waitlist').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.preventDefault();
            isB2B = btn.getAttribute('data-terminal') === 'b2b';
            
            document.body.style.animation = 'glitch-anim-1 0.2s 3';
            setTimeout(() => {
                document.body.style.animation = '';
                openTerminal();
            }, 600);
        });
    });

    function openTerminal() {
        terminal.style.display = 'flex';
        terminalOutput.innerHTML = '';
        terminalInputWrapper.style.display = 'none';
        terminalInput.value = '';
        
        if (isB2B) {
            terminal.style.color = 'var(--neon-red)';
            document.querySelector('.terminal-header').innerText = "CLOUDY BUSINESS // VENUE CLAIM PROTOCOL";
            document.querySelector('.cursor-block').style.background = 'var(--neon-red)';
            document.getElementById('terminal-input').style.textShadow = '0 0 10px var(--neon-red)';
        } else {
            terminal.style.color = 'var(--neon-blue)';
            document.querySelector('.terminal-header').innerText = "GHOST PROTOCOL // SECURE CONNECTION ESTABLISHED";
            document.querySelector('.cursor-block').style.background = 'var(--neon-blue)';
            document.getElementById('terminal-input').style.textShadow = '0 0 10px var(--neon-blue)';
        }

        const lines = [
            "Initiating secure handshake...",
            "Bypassing algorithm filters...",
            "Accessing Cloudy mainframe...",
            "Warning: Night is approaching.",
            "Please authenticate to secure your position."
        ];

        let delay = 0;
        lines.forEach((line, index) => {
            setTimeout(() => {
                const p = document.createElement('div');
                p.className = 'terminal-line';
                p.innerText = line;
                terminalOutput.appendChild(p);
                playTypeSound();
                if(index === lines.length - 1) {
                    setTimeout(() => {
                        terminalInputWrapper.style.display = 'flex';
                        terminalInput.focus();
                    }, 500);
                }
            }, delay);
            delay += 600;
        });
    }

    terminalInput.addEventListener('keydown', (e) => {
        playTypeSound();
        if(e.key === 'Enter' || e.keyCode === 13) {
            const handle = terminalInput.value.trim().toUpperCase();
            if(handle.length < 2) return;
            
            terminalInputWrapper.style.display = 'none';
            const p = document.createElement('div');
            p.className = 'terminal-line';
            p.innerText = `> ${handle}`;
            terminalOutput.appendChild(p);

            setTimeout(() => {
                const processLine = document.createElement('div');
                processLine.className = 'terminal-line';
                processLine.innerText = `Encrypting handle [${handle}]...`;
                terminalOutput.appendChild(processLine);
                playTypeSound();
            }, 500);

            setTimeout(() => {
                const queuePos = Math.floor(Math.random() * 1000) + 8000;
                const resultLine = document.createElement('div');
                resultLine.className = 'terminal-line';
                resultLine.style.color = '#fff';
                resultLine.style.marginTop = '2rem';
                resultLine.style.fontWeight = 'bold';
                
                if(isB2B) {
                    resultLine.innerText = `ACCESS GRANTED. VENUE ${handle} REGISTERED. PRIORITY QUEUE #${queuePos}. WE WILL CONTACT YOU SOON.`;
                } else {
                    resultLine.innerText = `ACCESS GRANTED. YOU ARE #${queuePos.toLocaleString()} IN THE GHOST PROTOCOL QUEUE.`;
                }
                
                terminalOutput.appendChild(resultLine);
                
                const closeNote = document.createElement('div');
                closeNote.className = 'terminal-line';
                closeNote.innerText = "\n(Press ESC to return to the grid)";
                terminalOutput.appendChild(closeNote);
                
                playFlareSound();
            }, 2000);
        }
    });

    document.addEventListener('keydown', (e) => {
        if(e.key === 'Escape' && terminal.style.display === 'flex') {
            terminal.style.display = 'none';
        }
    });

    // --- 7. Three.js: Globe & Particles ---
    const canvasContainer = document.getElementById('canvas-container');
    const scene = new THREE.Scene();
    scene.fog = new THREE.FogExp2(0x000000, 0.0015);
    const camera = new THREE.PerspectiveCamera(75, window.innerWidth / window.innerHeight, 0.1, 2000);
    camera.position.z = 200;
    const renderer = new THREE.WebGLRenderer({ alpha: true, antialias: false, powerPreference: "high-performance" });
    renderer.setSize(window.innerWidth, window.innerHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    canvasContainer.appendChild(renderer.domElement);

    const group = new THREE.Group();
    scene.add(group);

    const globeGeo = new THREE.SphereGeometry(80, 32, 32);
    const globeMat = new THREE.MeshBasicMaterial({ color: 0x8a2be2, wireframe: true, transparent: true, opacity: 0.15 });
    const globe = new THREE.Mesh(globeGeo, globeMat);
    group.add(globe);

    const pGeo = new THREE.BufferGeometry();
    const pCount = 10000;
    const pPos = new Float32Array(pCount * 3);
    const pCol = new Float32Array(pCount * 3);
    const c1 = new THREE.Color(0x00f0ff), c2 = new THREE.Color(0x8a2be2), c3 = new THREE.Color(0xff007f);

    for(let i = 0; i < pCount * 3; i+=3) {
        pPos[i] = (Math.random() - 0.5) * 800;
        pPos[i+1] = (Math.random() - 0.5) * 800;
        pPos[i+2] = (Math.random() - 0.5) * 1200 - 200;
        let mixColor = c1.clone();
        if(Math.random() > 0.6) mixColor = c2.clone();
        if(Math.random() > 0.9) mixColor = c3.clone();
        pCol[i] = mixColor.r; pCol[i+1] = mixColor.g; pCol[i+2] = mixColor.b;
    }
    pGeo.setAttribute('position', new THREE.BufferAttribute(pPos, 3));
    pGeo.setAttribute('color', new THREE.BufferAttribute(pCol, 3));

    const pCanvas = document.createElement('canvas'); pCanvas.width = 16; pCanvas.height = 16;
    const pCtx = pCanvas.getContext('2d');
    const pGrad = pCtx.createRadialGradient(8, 8, 0, 8, 8, 8);
    pGrad.addColorStop(0, 'rgba(255,255,255,1)'); pGrad.addColorStop(1, 'rgba(255,255,255,0)');
    pCtx.fillStyle = pGrad; pCtx.fillRect(0,0,16,16);
    
    const pMat = new THREE.PointsMaterial({ size: 2, vertexColors: true, map: new THREE.CanvasTexture(pCanvas), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false });
    const pMesh = new THREE.Points(pGeo, pMat);
    group.add(pMesh);

    const composer = new EffectComposer(renderer);
    composer.addPass(new RenderPass(scene, camera));
    const bloomPass = new UnrealBloomPass(new THREE.Vector2(window.innerWidth, window.innerHeight), 2.0, 0.4, 0.85);
    bloomPass.threshold = 0.1;
    composer.addPass(bloomPass);
    const rgbShiftPass = new ShaderPass(RGBShiftShader);
    composer.addPass(rgbShiftPass);

    const clock = new THREE.Clock();
    let targetX = 0, targetY = 0;

    function animate() {
        requestAnimationFrame(animate);
        const time = clock.getElapsedTime();
        targetX = (mouseX / window.innerWidth) * 2 - 1;
        targetY = -(mouseY / window.innerHeight) * 2 + 1;

        group.rotation.y = time * 0.05;
        globe.rotation.x = time * 0.02;
        
        camera.position.x += (targetX * 30 - camera.position.x) * 0.05;
        camera.position.y += (targetY * 30 - camera.position.y) * 0.05;

        let targetShift = 0.0015 + (mouseSpeed * 0.0001);
        if(targetShift > 0.02) targetShift = 0.02;
        rgbShiftPass.uniforms['amount'].value += (targetShift - rgbShiftPass.uniforms['amount'].value) * 0.1;
        rgbShiftPass.uniforms['angle'].value = time * 2;
        mouseSpeed *= 0.9;

        composer.render();
    }
    animate();

    window.addEventListener('resize', () => {
        camera.aspect = window.innerWidth / window.innerHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(window.innerWidth, window.innerHeight);
        composer.setSize(window.innerWidth, window.innerHeight);
        
        // Reset horizontal scroll pinning on resize
        if (window.innerWidth <= 768) {
            ScrollTrigger.refresh();
        }
    });

    // --- 8. GSAP ScrollTriggers ---
    function initGSAP() {
        gsap.registerPlugin(ScrollTrigger);

        gsap.to(camera.position, {
            z: -600,
            ease: "none",
            scrollTrigger: { trigger: "#main-content", start: "top top", end: "bottom bottom", scrub: 1 }
        });

        gsap.utils.toArray('.gs-manifesto').forEach((block, i) => {
            gsap.from(block, {
                scrollTrigger: { trigger: block, start: "top 80%", toggleActions: "play none none reverse" },
                x: i % 2 === 0 ? -100 : 100, opacity: 0, duration: 1.5, ease: "power3.out"
            });
        });

        // Only apply horizontal pinning on desktop
        if (!isMobile) {
            const horizontalContainer = document.querySelector('.horizontal-scroll-container');
            const panels = gsap.utils.toArray('.horizontal-panel');
            gsap.to(panels, {
                xPercent: -100 * (panels.length - 1),
                ease: "none",
                scrollTrigger: { trigger: ".horizontal-scroll-section", pin: true, scrub: 1, snap: 1 / (panels.length - 1), end: () => "+=" + horizontalContainer.offsetWidth }
            });
        }

        gsap.utils.toArray('.gs-reveal').forEach(elem => {
            gsap.from(elem, { scrollTrigger: { trigger: elem, start: "top 85%", toggleActions: "play none none reverse" }, y: 50, opacity: 0, duration: 1, ease: "power3.out" });
        });
    }
});
