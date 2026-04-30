document.addEventListener('DOMContentLoaded', () => {
    // Advanced scroll-triggered animations logic
    const animateOnScrollElements = document.querySelectorAll('.animate-on-scroll');

    const observerOptions = {
        root: null, // viewport
        rootMargin: '0px',
        threshold: 0.15 // Trigger when 15% of the element is visible for a more intentional feel
    };

    const observer = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target); // Stop observing once animated
            }
        });
    }, observerOptions);

    animateOnScrollElements.forEach(element => {
        observer.observe(element);
    });

    // Optional: Subtle 3D tilt effect on the Hero Mockup for desktop
    const mockup = document.querySelector('.hero-mockup');
    if (mockup && window.innerWidth > 768) {
        document.addEventListener('mousemove', (e) => {
            const xAxis = (window.innerWidth / 2 - e.pageX) / 25;
            const yAxis = (window.innerHeight / 2 - e.pageY) / 25;
            mockup.style.transform = `rotateY(${xAxis}deg) rotateX(${yAxis}deg)`;
        });
        document.addEventListener('mouseleave', () => {
            mockup.style.transform = `rotateY(0deg) rotateX(0deg)`;
        });
    }
});