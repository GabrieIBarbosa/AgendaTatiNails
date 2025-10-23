// wwwroot/js/agendamento-page.js 
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
    const serviceSelect = document.getElementById('service-select');
    const msgStep1 = document.getElementById('step-1-message');
    const msgStep2 = document.getElementById('step-2-message');
    const msgStep3 = document.getElementById('step-3-message');
    const container = document.querySelector('.agendamento-container');
    const isUserAuthenticated = container?.dataset.isAuthenticated === 'true'; // Adicionado ? para segurança

    // --- Variáveis de Estado ---
    let selectedDate = null;
    let selectedTime = null;
    let selectedService = null;
    const mockServiceDetails = {
        '1': { name: "Mão", price: "45", duration: 40, id: '1' },
        '2': { name: "Pé", price: "55", duration: 45, id: '2' },
        '3': { name: "Pé e Mão", price: "95", duration: 80, id: '3' }
    };
    const urlParams = new URLSearchParams(window.location.search);
    const serviceIdFromUrl = urlParams.get('serviceId');

    // --- Inicialização ---
    function inicializarServicos() {
        if (!serviceSelect) return;
        serviceSelect.innerHTML = '';
        Object.values(mockServiceDetails).forEach(service => {
            const option = document.createElement('option');
            option.value = service.id;
            option.textContent = `${service.name} (R$${service.price})`;
            serviceSelect.appendChild(option);
        });

        const initialServiceId = serviceIdFromUrl && mockServiceDetails[serviceIdFromUrl]
                               ? serviceIdFromUrl
                               : Object.keys(mockServiceDetails)[0];

        if (mockServiceDetails[initialServiceId]) { // Verifica se ID é válido
             serviceSelect.value = initialServiceId;
             selectedService = mockServiceDetails[initialServiceId];
        } else if (Object.keys(mockServiceDetails).length > 0) { // Fallback para o primeiro
             const firstId = Object.keys(mockServiceDetails)[0];
             serviceSelect.value = firstId;
             selectedService = mockServiceDetails[firstId];
        }


        serviceSelect.addEventListener('change', () => {
            clearAllMessages();
            const newServiceId = serviceSelect.value;
            selectedService = mockServiceDetails[newServiceId];

            if (stepCalendario && !stepCalendario.classList.contains('active')) {
                showStep(stepCalendario);
                if(calendarInput) calendarInput.value = '';
                showMessage(msgStep1, 'Serviço alterado. Selecione a data novamente.', 'error');
            }
        });
    }

    inicializarServicos();

    // --- Event Listeners ---
    if (calendarInput) {
        calendarInput.addEventListener('click', () => {
            try { calendarInput.showPicker(); }
            catch (error) { console.warn("showPicker() not supported."); }
        });
        calendarInput.addEventListener('change', () => clearAllMessages());
    }

    // Navegação Etapa 1 -> Etapa 2 (CHAMA O BACKEND)
    if (btnProximoHorarios) {
        btnProximoHorarios.addEventListener('click', async () => {
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
            const servicoId = selectedService.id;

            if (horariosGrid) horariosGrid.innerHTML = '<p>Buscando horários disponíveis...</p>';
            if (btnProximoConfirmacao) btnProximoConfirmacao.disabled = true;
            showStep(stepHorarios);

            try {
                const response = await fetch(`/Agendamento/GetHorariosDisponiveis?data=${selectedDate}&servicoId=${servicoId}`);

                if (!response.ok) {
                    const errorResult = await response.json().catch(() => ({ message: `Erro ${response.status} ao buscar horários.` }));
                    showMessage(msgStep2, errorResult.message);
                    if (horariosGrid) horariosGrid.innerHTML = '';
                    return;
                }

                const horariosDisponiveis = await response.json();
                if (horariosGrid) horariosGrid.innerHTML = ''; // Limpa o loading

                if (horariosDisponiveis.length === 0) {
                     const agora = new Date();
                     const hoje = agora.toISOString().split('T')[0];
                     const horaAtual = agora.getHours();
                     if (selectedDate === hoje && horaAtual >= 12) {
                         showMessage(msgStep2, 'Não há mais horários disponíveis para hoje.');
                     } else if (selectedDate === hoje && horaAtual < 12) {
                         showMessage(msgStep2, 'Não há horários disponíveis para a tarde neste dia.');
                     } else if (new Date(selectedDate) < new Date(hoje)) {
                          showMessage(msgStep2, 'Não é possível agendar em datas passadas.');
                     } else {
                         showMessage(msgStep2, 'Nenhum horário disponível para esta data e serviço.');
                     }
                    if (btnProximoConfirmacao) btnProximoConfirmacao.disabled = true;
                } else {
                    horariosDisponiveis.forEach(hora => {
                        const btn = document.createElement("button");
                        btn.textContent = hora;
                        btn.className = "btn-time";
                        btn.dataset.time = hora;
                        btn.addEventListener('click', (e) => {
                            clearAllMessages();
                            document.querySelectorAll('.btn-time').forEach(b => b.classList.remove('selected'));
                            e.target.classList.add('selected');
                            selectedTime = hora;
                            if (btnProximoConfirmacao) btnProximoConfirmacao.disabled = false;
                        });
                        if (horariosGrid) horariosGrid.appendChild(btn);
                    });
                    selectedTime = null;
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
            if (!selectedTime) { showMessage(msgStep2, "Selecione um horário."); return; }

            if (!isUserAuthenticated) {
                showMessage(msgStep2, 'Você precisa estar logado. Redirecionando...');
                setTimeout(() => {
                    window.location.href = `/Login/Index?ReturnUrl=${encodeURIComponent(window.location.pathname + window.location.search)}`;
                }, 1000);
                return;
            }

            // Preenche confirmação
            const confirmServiceEl = document.querySelector(".confirm-service");
            const confirmDateEl = document.querySelector(".confirm-date");
            const confirmTimeEl = document.querySelector(".confirm-time");
            const confirmPriceEl = document.querySelector(".confirm-price");

            if(confirmServiceEl) confirmServiceEl.textContent = selectedService?.name || 'N/A';
            if(confirmDateEl) confirmDateEl.textContent = selectedDate ? new Date(selectedDate + 'T00:00:00').toLocaleDateString('pt-BR') : 'N/A';
            if(confirmTimeEl) confirmTimeEl.textContent = selectedTime || 'N/A';
            if(confirmPriceEl) confirmPriceEl.textContent = selectedService ? `R$${selectedService.price}` : 'N/A';

            showStep(stepConfirmacao);
        });
    }

    // Submissão Final (AJAX)
    if (btnConfirmarAgendamento) {
        btnConfirmarAgendamento.addEventListener('click', async () => {
            if (!selectedTime || !selectedDate || !selectedService) {
                 showMessage(msgStep3, "Erro no agendamento. Tente novamente.");
                 return;
            }

            const agendamentoData = {
                ServicoId: selectedService.id,
                Data: selectedDate,
                Hora: selectedTime
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
                     showStep(stepHorarios); // Volta para escolher horário
                     // Idealmente, recarregaria os horários aqui
                     btnConfirmarAgendamento.disabled = false;
                     if (btnVoltarHorarios) btnVoltarHorarios.disabled = false;
                } else {
                     const errorResult = await response.json().catch(() => ({ message: 'Erro desconhecido.' }));
                     showMessage(msgStep3, `Erro: ${errorResult.message || 'Tente novamente.'}`);
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

// --- Funções Auxiliares (Definidas Fora do DOMContentLoaded) ---
function clearAllMessages() {
    document.querySelectorAll('.step-message-container').forEach(msg => {
        msg.textContent = '';
        msg.className = 'step-message-container';
    });
}

function showMessage(element, message, type = 'error') {
    clearAllMessages();
     if (element) { // Verifica se o elemento existe
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
    if (stepToShow) { // Verifica se o elemento existe
        stepToShow.classList.add('active');
    } else {
         console.error("Elemento de etapa não encontrado:", stepToShow);
    }
}