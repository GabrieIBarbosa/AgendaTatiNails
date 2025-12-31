# Agenda Tati Nails

> Sistema de agendamento desenvolvido como projeto final da disciplina de **Programação Orientada a Objetos (POO)**.

![Status](https://img.shields.io/badge/Status-Concluído-success)
![C#](https://img.shields.io/badge/C%23-Backend-239120?style=flat&logo=c-sharp&logoColor=white)
![ASP.NET](https://img.shields.io/badge/ASP.NET-MVC-purple)
![SQL Server](https://img.shields.io/badge/SQL_Server-Database-CC2927?style=flat&logo=microsoft-sql-server&logoColor=white)

## Sobre o Projeto

Este projeto foi desenvolvido como trabalho final da disciplina de Programação Orientada a Objetos (POO), com o objetivo de aplicar na prática conceitos como Classes, Herança, Encapsulamento e o padrão de arquitetura MVC.

O sistema nasceu de uma necessidade real: ajudar uma manicure a profissionalizar a gestão do seu salão, saindo da informalidade do caderno e do WhatsApp. Antes do sistema, o cenário enfrentado incluía diversos problemas que impactavam o negócio:
A troca de mensagens no WhatsApp tomava muito tempo produtivo da profissional, Ocorrências frequentes de esquecimento de horários ou "choque de agenda", Falta de controle sobre o lucro real. O dinheiro entrava  sem registro, dificultando saber quais serviços eram mais lucrativos ou quanto o salão faturou no mês, A cliente dependia que a manicure visualizasse a mensagem para confirmar um horário, o que muitas vezes demorava horas.

A Solução: Desenvolvi uma aplicação Web que automatiza todo esse fluxo. 

## Funcionalidades

O sistema foi dividido em dois módulos principais para organizar as permissões de acesso:

### 1. Área do Cliente (Público)
* **Agendamento Online:** O cliente seleciona o serviço desejado e o sistema mostra apenas os horários que comportam aquele serviço.
* **Histórico:** O cliente pode consultar seus agendamentos futuros.
* **Interface Responsiva:** Layout simples feito para ser acessado pelo celular.

### 2. Área do Administrador (Manicure)
* **Dashboard Visual:** Visão geral dos horários ocupados do dia.
* **Bloqueio de Horários:** Funcionalidade para o administrador fechar a agenda em dias de folga ou horários de almoço.
* **Controle Financeiro:** O sistema soma o valor dos serviços agendados, gerando um relatório simples de faturamento.

---

## Tecnologias e Conceitos Aplicados

*   **Organização e Estrutura (MVC):**
    Adotamos a arquitetura MVC para separar claramente as responsabilidades do sistema. Isso significa que a interface visual não se mistura com as regras de negócio, resultando em um código limpo, organizado e muito mais fácil de manter ou expandir no futuro.

*   **Backend (C# & ASP.NET):**
    Utilizamos C# para desenvolver toda a inteligência do sistema. O foco foi aplicar conceitos sólidos de **Programação Orientada a Objetos (POO)** para modelar corretamente o funcionamento do salão (como Clientes e Agendamentos) e garantir que a lógica de verificação de horários fosse precisa e segura.

*   **Banco de Dados (SQL Server):**
    Ao invés de utilizar ferramentas que automatizam todo o acesso aos dados, optamos por construir a comunicação com o banco manualmente (via **ADO.NET**), para dominar os fundamentos de comandos SQL.

*   **Interface e Usabilidade (Bootstrap):**
    O foco no Frontend foi a simplicidade. Utilizamos HTML e CSS com o framework Bootstrap para criar telas intuitivas e responsivas, garantindo que o cliente consiga realizar seu agendamento facilmente

---

## Autor

Projeto desenvolvido por **Gabriel Felipe Barbosa, Helio Augusto Vieira e João Pedro Prates**.

Estudantes de Análise e Desenvolvimento de Sistemas.

[LinkedIn](https://www.linkedin.com/in/gabriel-barbosa-2aa92a343/) | [Email](gabrielofcbarbosa22@gmail.com)
