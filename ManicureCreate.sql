create database manicureBD
go

use manicureBD
go

create table Usuarios
(
	usuarioId		int				identity		primary key,
	usuarioEmail	varchar(50)		not null,
	usuarioSenha	varchar(50)		not null,
	usuarioNome		varchar(50)		not null
)
go

create table Clientes
(
	clienteId		int				references Usuarios(usuarioID) primary key,
	clienteTelefone varchar(15)	
)
go

create table Colaboradores
(
	colabId			int				references Usuarios(usuarioId) primary key,
)
go

create table Servicos
(
	servicoId		int				identity		primary key,
	servicoDesc		varchar(100)	not null,
	servicoDuracao	int				not null,
	servicoPreco	money			not null,
	servicoStatus	int				not null		check(servicoStatus in (1,2))
)
go


create table Atendimentos
(
	atendId			int				identity		primary key,
	atendStatus		int				not null		check(atendStatus in (1,2,3)),
	atendDataAgend	datetime		not null,
	atendDataAtend	datetime		not null,
	atendObs		varchar(100)	null,
	atendPrecoFinal	money			null,
	idCliente		int				references		Clientes(clienteId),
	idColab			int				references		Colaboradores(colabId),
	pagData			datetime		not null,
	pagStatus		int				not null		check(pagStatus in (1,2)),
)
go

create table Horarios
(
	horarioId		int				identity		primary key,
	horarioStatus	int				not null		check(horarioStatus in (1,2)),
	horarioPeriodo	time			not null,
	horarioData		date			not null,
	idAtend			int				null			references Atendimentos(atendId)
)
go

create table ServicosAtend
(
	saDesconto		decimal(10,2)		null,
	idAtend			int				not null		references Atendimentos(atendId),
	idServico		int				not null		references Servicos(servicoId),
	primary key (idAtend, idServico)
)
go

create table FormasPagamento
(
	formaPagId			int				identity		primary key,
	formaPagTipo		int				not null		check(formaPagTipo in (1,2,3,4)),
	idAtendimento		int				not null		references Atendimentos(atendId)
)
go

-- Procedure para Colocar Horarios a partir da demanda --
-- ========= SCRIPT 2: CRIAR O PROCEDIMENTO =========

CREATE PROCEDURE sp_GarantirHorariosParaData
    @Data DATE
AS
BEGIN
    SET DATEFIRST 7; 
    
    -- 1. Se os horários já existem, não faz nada.
    IF EXISTS (SELECT 1 FROM Horarios WHERE horarioData = @Data)
    BEGIN
        RETURN;
    END

    -- 2. Se for um Sábado (7) ou Domingo (1), não faz nada.
    IF (DATEPART(WEEKDAY, @Data) IN (1, 7))
    BEGIN
        RETURN;
    END

    -- 3. Se chegou aqui, é um dia útil sem horários. Vamos criar!
    DECLARE @SlotAtual TIME = '08:00:00';
    DECLARE @FimDoDia TIME = '17:45:00';
    DECLARE @UltimoSlotManha TIME = '11:45:00';
    DECLARE @InicioTarde TIME = '13:30:00';

    WHILE @SlotAtual <= @FimDoDia
    BEGIN
        INSERT INTO Horarios (horarioStatus, horarioPeriodo, horarioData, idAtend)
        VALUES (1, @SlotAtual, @Data, NULL); -- 1 = Disponível

        IF (@SlotAtual = @UltimoSlotManha)
            SET @SlotAtual = @InicioTarde;
        ELSE
            SET @SlotAtual = DATEADD(MINUTE, 45, @SlotAtual);
    END
END
GO

-- Inserts --
-- 1.Insere os TRÊS serviços corretos (Opção B)
INSERT INTO Servicos (servicoDesc, servicoDuracao, servicoPreco, servicoStatus)
VALUES 
('Mão', 45, 45.00, 1),       -- ID 1 (1 slot de 45 min)
('Pé', 45, 55.00, 1),        -- ID 2 (1 slot de 45 min)
('Pé e Mão (Combo)', 90, 95.00, 1); -- ID 3 (2 slots de 90 min)

-- 2. Cadastra a "Tati" (Colaborador/Admin)
INSERT INTO Usuarios (usuarioEmail, usuarioSenha, usuarioNome)
VALUES ('tati@email.com', 'admin123', 'Tati (Profissional)');
DECLARE @IdColab INT = SCOPE_IDENTITY();
INSERT INTO Colaboradores (colabId) VALUES (@IdColab);

-- 3. Cadastra um "Cliente Teste"
INSERT INTO Usuarios (usuarioEmail, usuarioSenha, usuarioNome)
VALUES ('cliente@email.com', 'cliente123', 'Cliente Teste');
DECLARE @IdCliente INT = SCOPE_IDENTITY();
INSERT INTO Clientes (clienteId, clienteTelefone)
VALUES (@IdCliente, '11987654321');
GO


select * from Usuarios
go

select * from Atendimentos
go



