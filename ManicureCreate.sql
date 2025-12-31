CREATE DATABASE manicureBD;
GO
USE manicureBD;
GO

USE manicureBD;
GO




-- 1. Tabelas de Usuários e Perfis
CREATE TABLE Usuarios
(
    usuarioId    INT           IDENTITY    PRIMARY KEY,
    usuarioEmail VARCHAR(50)   NOT NULL UNIQUE,
    usuarioSenha VARCHAR(50)   NOT NULL,
    usuarioNome  VARCHAR(50)   NOT NULL
);
GO

CREATE TABLE Clientes
(
    clienteId       INT           REFERENCES Usuarios(usuarioID) PRIMARY KEY,
    clienteTelefone VARCHAR(15)
);
GO

CREATE TABLE Colaboradores
(
    colabId INT REFERENCES Usuarios(usuarioId) PRIMARY KEY
);
GO

-- 2. Tabela de Serviços
CREATE TABLE Servicos
(
    servicoId      INT           IDENTITY    PRIMARY KEY,
    servicoDesc    VARCHAR(100)  NOT NULL,
    servicoDuracao INT           NOT NULL,
    servicoPreco   MONEY         NOT NULL,
    servicoStatus  INT           NOT NULL    CHECK(servicoStatus IN (1,2))
);
GO

-- 3. Tabela de Atendimentos (ALTERADA PARA 1:N)
CREATE TABLE Atendimentos
(
    atendId            INT           IDENTITY    PRIMARY KEY,
    atendStatus        INT           NOT NULL    CHECK(atendStatus IN (1,2,3)),
    atendDataAgend     DATETIME      NOT NULL,
    atendDataAtend     DATETIME      NOT NULL,
    atendObs           VARCHAR(100)  NULL,
    atendPrecoFinal    MONEY         NULL,
    atendDataConclusao DATETIME      NULL,
    idCliente          INT           REFERENCES Clientes(clienteId),
    idColab            INT           REFERENCES Colaboradores(colabId),
    idServico          INT           NOT NULL REFERENCES Servicos(servicoId),

    pagData            DATETIME      NOT NULL,
    pagStatus          INT           NOT NULL    CHECK(pagStatus IN (1,2))
);
GO

-- 4. Tabela de Horários
CREATE TABLE Horarios
(
    horarioId      INT           IDENTITY    PRIMARY KEY,
    horarioStatus  INT           NOT NULL    CHECK (horarioStatus IN (1, 2, 3),
    horarioPeriodo TIME          NOT NULL,
    horarioData    DATE          NOT NULL,
    idAtend        INT           NULL        REFERENCES Atendimentos(atendId)
);
GO


CREATE TABLE FormasPagamento
(
    formaPagId    INT           IDENTITY    PRIMARY KEY,
    formaPagTipo  INT           NOT NULL    CHECK(formaPagTipo IN (1,2,3,4)),
    idAtendimento INT           NOT NULL    REFERENCES Atendimentos(atendId)
);
GO

-- 5. Stored Procedure 
CREATE PROCEDURE sp_GarantirHorariosParaData
    @Data DATE
AS
BEGIN
    SET DATEFIRST 7; 
    IF EXISTS (SELECT 1 FROM Horarios WHERE horarioData = @Data) RETURN;
    IF (DATEPART(WEEKDAY, @Data) IN (1, 7)) RETURN;

    DECLARE @SlotAtual TIME = '08:00:00';
    DECLARE @FimDoDia TIME = '17:45:00';
    DECLARE @UltimoSlotManha TIME = '11:45:00';
    DECLARE @InicioTarde TIME = '13:30:00';

    WHILE @SlotAtual <= @FimDoDia
    BEGIN
        INSERT INTO Horarios (horarioStatus, horarioPeriodo, horarioData, idAtend)
        VALUES (1, @SlotAtual, @Data, NULL); 

        IF (@SlotAtual = @UltimoSlotManha) SET @SlotAtual = @InicioTarde;
        ELSE SET @SlotAtual = DATEADD(MINUTE, 45, @SlotAtual);
    END
END
GO

-- 6. Dados Iniciais
INSERT INTO Servicos (servicoDesc, servicoDuracao, servicoPreco, servicoStatus)
VALUES 
('Mão', 45, 45.00, 1),
('Pé', 45, 55.00, 1),
('Pé e Mão (Combo)', 90, 95.00, 1);

INSERT INTO Usuarios (usuarioEmail, usuarioSenha, usuarioNome)
VALUES ('tati@email.com', 'admin123', 'Tati (Profissional)');
INSERT INTO Colaboradores (colabId) VALUES (SCOPE_IDENTITY());

INSERT INTO Usuarios (usuarioEmail, usuarioSenha, usuarioNome)
VALUES ('cliente@email.com', 'cliente123', 'Cliente Teste');
INSERT INTO Clientes (clienteId, clienteTelefone) VALUES (SCOPE_IDENTITY(), '11987654321');
GO