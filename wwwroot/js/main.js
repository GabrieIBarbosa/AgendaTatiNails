// wwwroot/js/main.js
document.addEventListener("DOMContentLoaded", () => {

    // --- [NOVO] Lógica do Menu Mobile ---
    const mobileMenuButton = document.getElementById('mobile-menu-toggle');
    const mainNavigation = document.getElementById('main-navigation');

    if (mobileMenuButton && mainNavigation) {
        mobileMenuButton.addEventListener('click', () => {
            const isExpanded = mobileMenuButton.getAttribute('aria-expanded') === 'true';

            // Alterna a classe que mostra/esconde o menu no CSS
            mainNavigation.classList.toggle('mobile-menu-open');

            // Atualiza os atributos ARIA para acessibilidade
            mobileMenuButton.setAttribute('aria-expanded', !isExpanded);
            mobileMenuButton.setAttribute('aria-label', isExpanded ? 'Abrir menu' : 'Fechar menu');

            // Troca o ícone (Opcional: Muda entre Hambúrguer e 'X')
             const icon = mobileMenuButton.querySelector('img');
             if (icon) {
                 icon.src = isExpanded
                    ? 'https://api.iconify.design/lucide-menu.svg' // Ícone Hambúrguer
                    : 'https://api.iconify.design/lucide-x.svg';     // Ícone 'X'
             }
        });
    }

    // --- Botão "Agendar agora" (Header) ---
    const btnAgendarHeader = document.querySelector("#btnAgendar");
    btnAgendarHeader?.addEventListener('click', (e) => {
        e.preventDefault();
        window.location.href = '/Agendamento';
    });

    // --- Botão "Começar agora" (Rodapé da Home) ---
    const btnComecarAgora = document.querySelector("#btnComecarAgora");
    btnComecarAgora?.addEventListener('click', (e) => {
        e.preventDefault();
        window.location.href = '/Agendamento';
    });

    // --- Links de Navegação (Scroll Suave) ---
    const navLinks = document.querySelectorAll('.main-nav a[href*="#"]');
    navLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            const targetId = this.hash;
            const onHomePage = window.location.pathname === '/' || window.location.pathname.toLowerCase() === '/home/index';

            if (onHomePage) {
                const targetElement = document.querySelector(targetId);
                if (targetElement) {
                    e.preventDefault();
                    targetElement.scrollIntoView({ behavior: 'smooth', block: 'start' });

                    // Fecha o menu mobile se estiver aberto após clicar em um link
                    if (mainNavigation && mainNavigation.classList.contains('mobile-menu-open')) {
                         mainNavigation.classList.remove('mobile-menu-open');
                         mobileMenuButton.setAttribute('aria-expanded', 'false');
                         mobileMenuButton.setAttribute('aria-label', 'Abrir menu');
                         const icon = mobileMenuButton.querySelector('img');
                         if (icon) icon.src = 'https://api.iconify.design/lucide-menu.svg';
                    }
                }
            }
            // Se não estiver na Home, deixa o link navegar normalmente
        });
    });

    // --- Botões "Agendar" dos Cards de Serviço (Home) ---
    const btnBookCards = document.querySelectorAll(".btn-book");
    btnBookCards.forEach(btn => {
        btn.addEventListener("click", () => {
            const serviceId = btn.dataset.serviceId;
            window.location.href = `/Agendamento?serviceId=${serviceId}`;
        });
    });

    // --- Dropdown de Usuário ---
    const userMenuButton = document.getElementById('user-menu-button');
    const userMenu = document.getElementById('user-dropdown-menu');

    if (userMenuButton && userMenu) {
        userMenuButton.addEventListener('click', function(e) {
             e.stopPropagation(); // Impede que o clique feche o menu imediatamente
            const isExpanded = userMenuButton.getAttribute('aria-expanded') === 'true' || false;
            userMenuButton.setAttribute('aria-expanded', !isExpanded);
            userMenu.classList.toggle('hidden');
        });

        document.addEventListener('click', function(event) {
            if (userMenu && !userMenu.classList.contains('hidden') &&
                !userMenuButton.contains(event.target) &&
                !userMenu.contains(event.target))
             {
                userMenuButton.setAttribute('aria-expanded', 'false');
                userMenu.classList.add('hidden');
            }
        });
    }
});