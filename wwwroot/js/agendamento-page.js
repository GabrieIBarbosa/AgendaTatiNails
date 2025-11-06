document.addEventListener("DOMContentLoaded", () => {
    // --- Seletores de Elementos ---
    const stepCalendario = document.getElementById('step-calendario');
    const stepHorarios = document.getElementById('step-horarios');
    const stepConfirmacao = document.getElementById('step-confirmacao');

    const btnProximoHorarios = document.getElementById('btnProximoHorarios');
    const btnProximoConfirmacao = document.getElementById('btnProximoConfirmacao');
    const btnConfirmarAgendamento = document.getElementById('btnConfirmarAgendamento');
    const btnVoltarCalendario = document.getElementById('btnVoltarCalendario');
    const btnVoltarHorarios = document.getElementById('btnVoltarHorarios');

    const calendarInput = document.getElementById('calendar-date');
    const horariosGrid = document.getElementById('horariosGrid');
    
    // --- ESTA É A LÓGICA CORRETA: 'service-select' ---
    const serviceSelect = document.getElementById('service-select');
    
    const msgStep1 = document.getElementById('step-1-message');
    const msgStep2 = document.getElementById('step-2-message');
    const msgStep3 = document.getElementById('step-3-message');

    const container = document.querySelector('.agendamento-container');
    const isUserAuthenticated = container?.dataset.isAuthenticated === 'true';

    // --- Variáveis de Estado (para serviço ÚNICO) ---
    let selectedDate = null;
    let selectedHorarioId = null; 
    let selectedHorarioStr = null; 
    let selectedService = null; // Um objeto { id, name, price, duration }

    // --- Inicialização ---
    function atualizarServicoSelecionado() {
        if (!serviceSelect || serviceSelect.selectedIndex < 0) {
            selectedService = null;
            return;
        }
        const option = serviceSelect.options[serviceSelect.selectedIndex];
        if (!option) {
            selectedService = null;
            return;
        }
        selectedService = {
            id: option.value,
            name: option.dataset.name,
            price: parseFloat(option.dataset.price),
            duration: parseInt(option.dataset.duration)
        };
    }

    // Inicializa no carregamento
    atualizarServicoSelecionado(); 

    // Event listener para quando o serviço mudar
    serviceSelect.addEventListener('change', () => {
        atualizarServicoSelecionado();
        // Força o usuário a re-selecionar a data/hora
        if (!stepCalendario.classList.contains('active')) {
            showStep(stepCalendario);
            if(calendarInput) calendarInput.value = '';
            if(horariosGrid) horariosGrid.innerHTML = '';
            showMessage(msgStep1, 'Serviço alterado. Selecione a data novamente.', 'error');
        } else {
            clearAllMessages();
        }
    });

    if (calendarInput) {
        calendarInput.addEventListener('change', () => clearAllMessages());
    }

    // Navegação Etapa 1 -> Etapa 2 (CHAMA O BACKEND)
    if (btnProximoHorarios) {
        btnProximoHorarios.addEventListener('click', async () => {
            
            // Validações
            if (!calendarInput || !calendarInput.value) {
                showMessage(msgStep1, "Selecione uma data para continuar!");
                return;
            }
            if (!selectedService) {
                showMessage(msgStep1, "Selecione um serviço válido.");
                return;
            }

            clearAllMessages();
            selectedDate = calendarInput.value; // YYYY-MM-DD
            
            // --- LÓGICA CORRETA ---
            // Pega a duração do serviço ÚNICO selecionado
            const duracaoTotal = selectedService.duration; 

            if (horariosGrid) horariosGrid.innerHTML = '<p>Buscando horários disponíveis...</p>';
            if (btnProximoConfirmacao) btnProximoConfirmacao.disabled = true;
            showStep(stepHorarios);

            try {
                // --- LÓGICA CORRETA ---
                // Envia a 'duracaoTotal' (ex: 45 ou 90) para o C#
                const fetchUrl = `/Agendamento/GetHorariosDisponiveis?data=${selectedDate}&duracaoTotal=${duracaoTotal}`;
                const response = await fetch(fetchUrl);

                if (!response.ok) {
                    const errorResult = await response.json().catch(() => ({ message: `Erro ${response.status} ao buscar horários.` }));
                    showMessage(msgStep2, errorResult.message);
                    if (horariosGrid) horariosGrid.innerHTML = '';
                    return;
                }
                
                // O C# (que já corrigimos) filtra os slots e envia a lista correta
                const horariosDisponiveis = await response.json();
                if (horariosGrid) horariosGrid.innerHTML = ''; 

                if (horariosDisponiveis.length === 0) {
                    showMessage(msgStep2, 'Nenhum horário disponível para esta data e serviço.');
                    if (btnProximoConfirmacao) btnProximoConfirmacao.disabled = true;
                } else {
                    horariosDisponiveis.forEach(h => {
                        const btn = document.createElement("button");
                        btn.textContent = h.horario;
                        btn.className = "btn-time";
                        btn.dataset.horarioId = h.horarioId; 
                        btn.dataset.horarioStr = h.horario; 
                        
                        btn.addEventListener('click', (e) => {
                            clearAllMessages();
                            document.querySelectorAll('.btn-time').forEach(b => b.classList.remove('selected'));
                            e.target.classList.add('selected');
                            
                            selectedHorarioId = e.target.dataset.horarioId;
                            selectedHorarioStr = e.target.dataset.horarioStr;
                            
                            if (btnProximoConfirmacao) btnProximoConfirmacao.disabled = false;
                        });
                        if (horariosGrid) horariosGrid.appendChild(btn);
                    });
                    selectedHorarioId = null;
                    selectedHorarioStr = null;
                    if (btnProximoConfirmacao) btnProximoConfirmacao.disabled = true;
                }

            } catch (error) {
                console.error("Erro ao buscar horários:", error);
                showMessage(msgStep2, "Erro de conexão ao buscar horários. Tente novamente.");
                if (horariosGrid) horariosGrid.innerHTML = '';
            }
        });
    }

    // Navegação Etapa 2 -> Etapa 3
    if (btnProximoConfirmacao) {
        btnProximoConfirmacao.addEventListener('click', () => {
            if (!selectedHorarioId) { showMessage(msgStep2, "Selecione um horário."); return; }

            if (!isUserAuthenticated) {
                // (Lógica de redirecionamento)
                showMessage(msgStep2, 'Você precisa estar logado. Redirecionando...');
                setTimeout(() => {
                    window.location.href = `/Login/Index?ReturnUrl=${encodeURIComponent(window.location.pathname + window.location.search)}`;
                }, 1000);
                return;
            }

            // --- LÓGICA CORRETA: Preencher com Serviço Único ---
            const confirmServiceEl = document.querySelector(".confirm-service");
            const confirmDateEl = document.querySelector(".confirm-date");
            const confirmTimeEl = document.querySelector(".confirm-time");
            const confirmPriceEl = document.querySelector(".confirm-price");

            if(confirmServiceEl) confirmServiceEl.textContent = selectedService.name;
            if(confirmDateEl) confirmDateEl.textContent = selectedDate ? new Date(selectedDate + 'T00:00:00').toLocaleDateString('pt-BR') : 'N/A';
            if(confirmTimeEl) confirmTimeEl.textContent = selectedHorarioStr || 'N/A';
            if(confirmPriceEl) confirmPriceEl.textContent = `R$ ${selectedService.price.toFixed(2)}`;

            showStep(stepConfirmacao);
        });
    }

    // Submissão Final (AJAX)
    if (btnConfirmarAgendamento) {
        btnConfirmarAgendamento.addEventListener('click', async () => {
            
            if (!selectedHorarioId || !selectedService) {
                showMessage(msgStep3, "Erro no agendamento. Tente novamente.");
                return;
            }

            // --- LÓGICA : JSON com 'servicoId' (int) ---
            const agendamentoData = {
                horarioId: parseInt(selectedHorarioId),
                servicoId: parseInt(selectedService.id)
            };

            btnConfirmarAgendamento.disabled = true;
            if (btnVoltarHorarios) btnVoltarHorarios.disabled = true;

            try {
                const response = await fetch('/Agendamento/CriarAgendamento', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(agendamentoData)
                });

                if (response.ok) {
                    showMessage(msgStep3, 'Agendamento confirmado! Redirecionando...', 'success');
                    setTimeout(() => { window.location.href = '/Servico/ListaServico'; }, 1000);
                    
                } else if (response.status === 401) {
                    showMessage(msgStep3, 'Sua sessão expirou. Redirecionando...');
                    setTimeout(() => { window.location.href = '/Login/Index'; }, 1000);

                } else if (response.status === 409) { 
                    const errorResult = await response.json().catch(() => ({ message: 'Este horário não está mais disponível.' }));
                    showMessage(msgStep3, errorResult.message);
                    showStep(stepHorarios); 
                    btnConfirmarAgendamento.disabled = false;
                    if (btnVoltarHorarios) btnVoltarHorarios.disabled = false;
                
                } else {
                    const errorText = await response.text(); 
                    console.error("Erro do servidor:", errorText);
                    showMessage(msgStep3, `Erro ao salvar: O servidor ainda não está pronto para salvar (CRUD).`);
                    btnConfirmarAgendamento.disabled = false;
                    if (btnVoltarHorarios) btnVoltarHorarios.disabled = false;
                }
            } catch (error) {
                console.error('Erro na requisição:', error);
                showMessage(msgStep3, 'Erro de conexão. Verifique o console.');
                btnConfirmarAgendamento.disabled = false;
                if (btnVoltarHorarios) btnVoltarHorarios.disabled = false;
            }
        });
    }

    // Botões de Voltar
    if (btnVoltarCalendario) btnVoltarCalendario.addEventListener('click', () => showStep(stepCalendario));
    if (btnVoltarHorarios) btnVoltarHorarios.addEventListener('click', () => showStep(stepHorarios));
});

// --- Funções Auxiliares ---
function clearAllMessages() {
    document.querySelectorAll('.step-message-container').forEach(msg => {
        msg.textContent = '';
        msg.className = 'step-message-container';
    });
}

function showMessage(element, message, type = 'error') {
    clearAllMessages();
    if (element) { 
        element.textContent = message;
        element.className = 'step-message-container active ' + type;
    } else {
        console.error("Elemento de mensagem não encontrado:", element);
    }
}

function showStep(stepToShow) {
    clearAllMessages();
    document.querySelectorAll('.agendamento-step').forEach(step => {
        step.classList.remove('active');
    });
    if (stepToShow) { 
        stepToShow.classList.add('active');
    } else {
        console.error("Elemento de etapa não encontrado:", stepToShow);
    }
}