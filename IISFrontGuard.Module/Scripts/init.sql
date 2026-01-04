USE [master]
GO

CREATE DATABASE [IISFrontGuard]
GO

USE [IISFrontGuard]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Action](
	[Id] [tinyint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[Description] [nvarchar](255) NULL,
	[CreatedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AppEntity](
	[Id] [uniqueidentifier] NOT NULL,
	[AppName] [nvarchar](255) NOT NULL,
	[AppDescription] [nvarchar](max) NULL,
	[Host] [nvarchar](128) NOT NULL,
	[CreationDate] [datetime] NULL,
	[TokenExpirationDurationHr] [tinyint] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Field](
	[Id] [tinyint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[NormalizedName] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](255) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UX_Field_Normalized] UNIQUE NONCLUSTERED 
(
	[NormalizedName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Operator](
	[Id] [tinyint] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[NormalizedName] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](255) NULL,
	[CreatedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UX_Operator_Normalized] UNIQUE NONCLUSTERED 
(
	[NormalizedName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RequestContext](
	[Id] [uniqueidentifier] NOT NULL,
	[RayId] [uniqueidentifier] NOT NULL,
	[HostName] [nvarchar](255) NULL,
	[AppId] [uniqueidentifier] NULL,
	[IPAddress] [nvarchar](45) NULL,
	[Protocol] [nvarchar](10) NULL,
	[Referrer] [nvarchar](2048) NULL,
	[HttpMethod] [nvarchar](10) NULL,
	[HttpVersion] [nvarchar](20) NULL,
	[UserAgent] [nvarchar](1024) NULL,
	[XForwardedFor] [nvarchar](255) NULL,
	[MimeType] [nvarchar](255) NULL,
	[UrlFull] [nvarchar](2048) NULL,
	[UrlPath] [nvarchar](2048) NULL,
	[UrlPathAndQuery] [nvarchar](2048) NULL,
	[UrlQueryString] [nvarchar](2048) NULL,
	[RuleId] [int] NULL,
	[ActionId] [tinyint] NULL,
	[CreatedAt] [datetime2](3) NOT NULL,
	[RequestBody] [nvarchar](max) NULL,
	[CountryIso2] [char](2) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ResponseContext](
	[Id] [uniqueidentifier] NOT NULL,
	[Url] [nvarchar](max) NOT NULL,
	[HttpMethod] [nvarchar](50) NOT NULL,
	[ResponseTime] [bigint] NOT NULL,
	[Timestamp] [datetime] NOT NULL,
	[RayId] [uniqueidentifier] NULL,
	[StatusCode] [smallint] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WafConditionEntity](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[FieldId] [tinyint] NOT NULL,
	[OperatorId] [tinyint] NOT NULL,
	[Valor] [nvarchar](1000) NULL,
	[LogicOperator] [tinyint] NULL,
	[WafRuleEntityId] [int] NOT NULL,
	[FieldName] [varchar](100) NULL,
	[ConditionOrder] [int] NOT NULL,
	[CreationDate] [datetime] NULL,
 CONSTRAINT [PK__WafCondi__3214EC079048517C] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WafRuleEntity](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Nombre] [nvarchar](255) NULL,
	[ActionId] [tinyint] NOT NULL,
	[AppId] [uniqueidentifier] NOT NULL,
	[Prioridad] [int] NULL,
	[Habilitado] [bit] NULL,
	[CreationDate] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Action] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AppEntity] ADD  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[AppEntity] ADD  DEFAULT (getdate()) FOR [CreationDate]
GO
ALTER TABLE [dbo].[AppEntity] ADD  DEFAULT ((12)) FOR [TokenExpirationDurationHr]
GO
ALTER TABLE [dbo].[Field] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Operator] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[RequestContext] ADD  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[RequestContext] ADD  DEFAULT (sysutcdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ResponseContext] ADD  DEFAULT (newid()) FOR [Id]
GO
ALTER TABLE [dbo].[WafConditionEntity] ADD  DEFAULT ((1)) FOR [LogicOperator]
GO
ALTER TABLE [dbo].[WafConditionEntity] ADD  DEFAULT ((0)) FOR [ConditionOrder]
GO
ALTER TABLE [dbo].[WafConditionEntity] ADD  DEFAULT (getdate()) FOR [CreationDate]
GO
ALTER TABLE [dbo].[WafRuleEntity] ADD  DEFAULT ((0)) FOR [Prioridad]
GO
ALTER TABLE [dbo].[WafRuleEntity] ADD  DEFAULT ((1)) FOR [Habilitado]
GO
ALTER TABLE [dbo].[WafRuleEntity] ADD  DEFAULT (getdate()) FOR [CreationDate]
GO
ALTER TABLE [dbo].[RequestContext]  WITH CHECK ADD  CONSTRAINT [FK_RequestContext_ActionId] FOREIGN KEY([ActionId])
REFERENCES [dbo].[Action] ([Id])
GO
ALTER TABLE [dbo].[RequestContext] CHECK CONSTRAINT [FK_RequestContext_ActionId]
GO
ALTER TABLE [dbo].[RequestContext]  WITH CHECK ADD  CONSTRAINT [FK_RequestContext_AppId] FOREIGN KEY([AppId])
REFERENCES [dbo].[AppEntity] ([Id])
GO
ALTER TABLE [dbo].[RequestContext] CHECK CONSTRAINT [FK_RequestContext_AppId]
GO
ALTER TABLE [dbo].[RequestContext]  WITH CHECK ADD  CONSTRAINT [FK_RequestContext_RuleId] FOREIGN KEY([RuleId])
REFERENCES [dbo].[WafRuleEntity] ([Id])
GO
ALTER TABLE [dbo].[RequestContext] CHECK CONSTRAINT [FK_RequestContext_RuleId]
GO
ALTER TABLE [dbo].[WafConditionEntity]  WITH CHECK ADD FOREIGN KEY([FieldId])
REFERENCES [dbo].[Field] ([Id])
GO
ALTER TABLE [dbo].[WafConditionEntity]  WITH CHECK ADD FOREIGN KEY([OperatorId])
REFERENCES [dbo].[Operator] ([Id])
GO
ALTER TABLE [dbo].[WafConditionEntity]  WITH CHECK ADD FOREIGN KEY([WafRuleEntityId])
REFERENCES [dbo].[WafRuleEntity] ([Id])
GO
ALTER TABLE [dbo].[WafRuleEntity]  WITH CHECK ADD  CONSTRAINT [FK_WafRuleEntity_ActionId] FOREIGN KEY([ActionId])
REFERENCES [dbo].[Action] ([Id])
GO
ALTER TABLE [dbo].[WafRuleEntity] CHECK CONSTRAINT [FK_WafRuleEntity_ActionId]
GO
ALTER TABLE [dbo].[WafRuleEntity]  WITH CHECK ADD  CONSTRAINT [FK_WafRuleEntity_AppId] FOREIGN KEY([AppId])
REFERENCES [dbo].[AppEntity] ([Id])
GO
ALTER TABLE [dbo].[WafRuleEntity] CHECK CONSTRAINT [FK_WafRuleEntity_AppId]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[InsertRequestContext]
(
    @RayId              UNIQUEIDENTIFIER,
    @HostName           NVARCHAR(255)    = NULL,
    @AppId              UNIQUEIDENTIFIER = NULL,
    @IPAddress          NVARCHAR(45)     = NULL,
    @Protocol           NVARCHAR(10)     = NULL,
    @Referrer           NVARCHAR(2048)   = NULL,
    @HttpMethod         NVARCHAR(10)     = NULL,
    @HttpVersion        NVARCHAR(20)     = NULL,
    @UserAgent          NVARCHAR(1024)   = NULL,
    @XForwardedFor      NVARCHAR(255)    = NULL,
    @MimeType           NVARCHAR(255)    = NULL,
    @UrlFull            NVARCHAR(2048)   = NULL,
    @UrlPath            NVARCHAR(2048)   = NULL,
    @UrlPathAndQuery    NVARCHAR(2048)   = NULL,
    @UrlQueryString     NVARCHAR(2048)   = NULL,
    @RuleId             INT              = NULL,
    @ActionId           TINYINT          = NULL,
    @RequestBody        NVARCHAR(MAX)    = NULL,
    @CountryIso2        CHAR(2)          = NULL,

    -- Optional overrides (table has defaults)
    @Id                 UNIQUEIDENTIFIER = NULL,
    @CreatedAt          DATETIME2(3)     = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.RequestContext
    (
        Id,
        RayId,
        HostName,
        AppId,
        IPAddress,
        Protocol,
        Referrer,
        HttpMethod,
        HttpVersion,
        UserAgent,
        XForwardedFor,
        MimeType,
        UrlFull,
        UrlPath,
        UrlPathAndQuery,
        UrlQueryString,
        RuleId,
        ActionId,
        CreatedAt,
        RequestBody,
        CountryIso2
    )
    VALUES
    (
        COALESCE(@Id, NEWID()),
        @RayId,
        @HostName,
        @AppId,
        @IPAddress,
        @Protocol,
        @Referrer,
        @HttpMethod,
        @HttpVersion,
        @UserAgent,
        @XForwardedFor,
        @MimeType,
        @UrlFull,
        @UrlPath,
        @UrlPathAndQuery,
        @UrlQueryString,
        @RuleId,
        @ActionId,
        COALESCE(@CreatedAt, SYSUTCDATETIME()),
        @RequestBody,
        @CountryIso2
    );
END;
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE   PROCEDURE [dbo].[InsertResponseContext]
(
    @Url          nvarchar(max),
    @HttpMethod   nvarchar(50),
    @ResponseTime bigint,
    @Timestamp    datetime,
    @RayId    uniqueidentifier = NULL,
    @StatusCode   smallint = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ResponseContext
    (Url, HttpMethod, ResponseTime, [Timestamp], RayId, StatusCode)
    VALUES
    (@Url, @HttpMethod, @ResponseTime, @Timestamp, @RayId, @StatusCode);
END;
GO
USE [master]
GO
ALTER DATABASE [IISFrontGuard] SET  READ_WRITE 
GO

--Feed the tables with initial data
USE [IISFrontGuard]
GO
SET IDENTITY_INSERT [dbo].[Action] ON 
GO
INSERT [dbo].[Action] ([Id], [Name], [Description], [CreatedAt]) VALUES 
(1, N'Skip', N'Allow request and skip further WAF processing', GETDATE()),
(2, N'Block', N'Block the request immediately', GETDATE()),
(3, N'Managed Challenge', N'Apply managed (automatic) challenge', GETDATE()),
(4, N'Interactive Challenge', N'Apply interactive challenge (e.g. captcha / JS)', GETDATE()),
(5, N'Log', N'Log request without enforcement', GETDATE()),
(6, N'Traffic', N'Traffic', GETDATE())
GO
SET IDENTITY_INSERT [dbo].[Action] OFF
GO

SET IDENTITY_INSERT [dbo].[Field] ON 
GO
INSERT [dbo].[Field] ([Id], [Name], [NormalizedName], [Description], [CreatedAt]) VALUES 
(1, N'cookie', N'COOKIE', N'HTTP cookie value', GETDATE()),
(2, N'hostname', N'HOSTNAME', N'Request host name', GETDATE()),
(3, N'ip', N'IP', N'Client IP address', GETDATE()),
(4, N'ip range', N'IP_RANGE', N'Client IP range', GETDATE()),
(5, N'protocol', N'PROTOCOL', N'Request protocol (HTTP / HTTPS)', GETDATE()),
(6, N'referrer', N'REFERRER', N'HTTP referrer header', GETDATE()),
(7, N'method', N'METHOD', N'HTTP method', GETDATE()),
(8, N'http version', N'HTTP_VERSION', N'HTTP protocol version', GETDATE()),
(9, N'user-agent', N'USER_AGENT', N'User-Agent header', GETDATE()),
(10, N'x-forwarded-for', N'X_FORWARDED_FOR', N'X-Forwarded-For header', GETDATE()),
(11, N'mime type', N'MIME_TYPE', N'Request MIME type', GETDATE()),
(12, N'Absolute Uri', N'Absolute_Uri', N'Full request URL', GETDATE()),
(13, N'Absolute Path', N'Absolute_Path', N'Request URL without query string', GETDATE()),
(14, N'Path And Query', N'Path_And_Query', N'Request URL path', GETDATE()),
(15, N'url querystring', N'URL_QUERYSTRING', N'Request URL query string', GETDATE()),
(16, N'header', N'HEADER', N'Specific HTTP header (requires header name)', GETDATE()),
(17, N'content type', N'CONTENT_TYPE', N'Request Content-Type header', GETDATE()),
(18, N'body', N'BODY', N'Raw request body (captured conditionally; use with caution)', GETDATE()),
(19, N'body length', N'BODY_LENGTH', N'Request body size in bytes (numeric operators only)', GETDATE()),
(20, N'country', N'COUNTRY', N'Request country (ISO 3166-1 alpha-2)', GETDATE()),
(21, N'country-iso2', N'COUNTRY_ISO2', N'Request country (ISO 3166-1 alpha-2)', GETDATE()),
(22, N'continent', N'CONTINENT', N'Request continent (ISO 3166-1 alpha-2)', GETDATE()),
(23, N'ip-cf-connecting-ip', N'CF_CONNECTING_IP', N'The most reliable header for the real client IP. Set on every request.', GETDATE()),
(24, N'ip-x-forwarded-for', N'IP_X_FORWARDED_FOR', N'Standard proxy header. Cloudflare appends the client IP to the list.', GETDATE()),
(25, N'ip-cf-connecting-ip', N'TRUE_CLIENT_IP', N'The original visitor IP. Used when Cloudflare is configured to pass the real IP explicitly.', GETDATE())
GO
SET IDENTITY_INSERT [dbo].[Field] OFF
GO

SET IDENTITY_INSERT [dbo].[Operator] ON 
GO
INSERT [dbo].[Operator] ([Id], [Name], [NormalizedName], [Description], [CreatedAt]) VALUES 
(1, N'equals', N'EQUALS', N'Value is equal to the target', GETDATE()),
(2, N'does not equal', N'NOT_EQUALS', N'Value is not equal to the target', GETDATE()),
(3, N'contains', N'CONTAINS', N'Value contains the target', GETDATE()),
(4, N'does not contain', N'NOT_CONTAINS', N'Value does not contain the target', GETDATE()),
(5, N'matches regex', N'REGEX_MATCH', N'Value matches the regular expression', GETDATE()),
(6, N'does not match regex', N'REGEX_NOT_MATCH', N'Value does not match the regular expression', GETDATE()),
(7, N'starts with', N'STARTS_WITH', N'Value starts with the target', GETDATE()),
(8, N'does not start with', N'NOT_STARTS_WITH', N'Value does not start with the target', GETDATE()),
(9, N'ends with', N'ENDS_WITH', N'Value ends with the target', GETDATE()),
(10, N'does not end with', N'NOT_ENDS_WITH', N'Value does not end with the target', GETDATE()),
(11, N'is in', N'IN', N'Value is in the provided set', GETDATE()),
(12, N'is not in', N'NOT_IN', N'Value is not in the provided set', GETDATE()),
(13, N'is in list', N'IN_LIST', N'Value is contained in a predefined list', GETDATE()),
(14, N'is not in list', N'NOT_IN_LIST', N'Value is not contained in a predefined list', GETDATE()),
(15, N'is ip in range', N'IP_IN_RANGE', N'IP address is within the specified range', GETDATE()),
(16, N'is ip not in range', N'IP_NOT_IN_RANGE', N'IP address is outside the specified range', GETDATE()),
(17, N'greater than', N'GREATER_THAN', N'Value is greater than the target (numeric only)', GETDATE()),
(18, N'less than', N'LESS_THAN', N'Value is less than the target (numeric only)', GETDATE()),
(19, N'greater than or equal to', N'GREATER_THAN_OR_EQUAL', N'Value is greater than or equal to the target (numeric only)', GETDATE()),
(20, N'less than or equal to', N'LESS_THAN_OR_EQUAL', N'Value is less than or equal to the target (numeric only)', GETDATE()),
(21, N'is present', N'IS_PRESENT', N'Value is present (has any value)', GETDATE()),
(22, N'is not present', N'IS_NOT_PRESENT', N'Value is not present (is empty)', GETDATE())
GO
SET IDENTITY_INSERT [dbo].[Operator] OFF
GO

-- Create an AppEntity for localhost testing
INSERT [dbo].[AppEntity] ([Id], [AppName], [AppDescription], [Host], [CreationDate], [TokenExpirationDurationHr]) VALUES (NEWID(), N'Localhost App', N'Test application for localhost', N'localhost', GETDATE(), 12)
GO

-- Retrieve the Id of the newly created AppEntity
DECLARE @LocalAppId UNIQUEIDENTIFIER
SELECT TOP 1 @LocalAppId = [Id] FROM [dbo].[AppEntity] WHERE [Host] = N'localhost'

-- Insert a rule for Interactive Challenge as an example on localhost using the newly created AppEntity
INSERT [dbo].[WafRuleEntity] ([Nombre], [ActionId], [AppId], [Prioridad], [Habilitado], [CreationDate]) 
VALUES (N'Interactive Challenge', 4, @LocalAppId, 0, 1, GETDATE())