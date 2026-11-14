-- Role: Creates platform and knowledge tables for explicit reviewed SQL provisioning; generated offline, not applied.
CREATE TABLE [AiAdmissionGates] (
    [Id] nvarchar(256) NOT NULL,
    [MinuteStart] datetimeoffset NOT NULL,
    [MinuteCount] int NOT NULL,
    [DayStart] datetimeoffset NOT NULL,
    [DayCount] int NOT NULL,
    [DayTokens] bigint NOT NULL,
    [Revision] bigint NOT NULL,
    CONSTRAINT [PK_AiAdmissionGates] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiConversations] (
    [Id] nvarchar(256) NOT NULL,
    [AssistantId] nvarchar(256) NOT NULL,
    [OwnerHash] nvarchar(256) NOT NULL,
    [AccessFingerprint] nvarchar(256) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ExpiresAt] datetimeoffset NOT NULL,
    [LeaseOwner] nvarchar(256) NULL,
    [LeaseUntil] datetimeoffset NULL,
    [Revision] bigint NOT NULL,
    CONSTRAINT [PK_AiConversations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiDataProtectionKeys] (
    [Id] int NOT NULL IDENTITY,
    [FriendlyName] nvarchar(256) NOT NULL,
    [ProtectedXml] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AiDataProtectionKeys] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiSecurityAudit] (
    [Id] nvarchar(256) NOT NULL,
    [AssistantId] nvarchar(256) NOT NULL,
    [SubjectHash] nvarchar(256) NOT NULL,
    [TraceId] nvarchar(256) NOT NULL,
    [Operation] nvarchar(256) NOT NULL,
    [Outcome] nvarchar(256) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiSecurityAudit] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiUsage] (
    [Id] nvarchar(256) NOT NULL,
    [AssistantId] nvarchar(256) NOT NULL,
    [SubjectHash] nvarchar(256) NOT NULL,
    [Operation] nvarchar(256) NOT NULL,
    [TraceId] nvarchar(256) NOT NULL,
    [Model] nvarchar(256) NOT NULL,
    [InputTokens] int NULL,
    [OutputTokens] int NULL,
    [TotalTokens] int NULL,
    [EstimatedCost] decimal(18,8) NULL,
    [LatencyMs] bigint NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiUsage] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiMessages] (
    [Id] nvarchar(256) NOT NULL,
    [ConversationId] nvarchar(256) NOT NULL,
    [Role] nvarchar(256) NOT NULL,
    [ProtectedText] nvarchar(max) NOT NULL,
    [ProtectedSources] nvarchar(max) NOT NULL,
    [ProtectedEvidence] nvarchar(max) NOT NULL,
    [Refused] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiMessages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiMessages_AiConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiConversations] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AiTurns] (
    [Id] nvarchar(256) NOT NULL,
    [ConversationId] nvarchar(256) NOT NULL,
    [ClientRequestId] nvarchar(256) NOT NULL,
    [QuestionHash] nvarchar(256) NOT NULL,
    [State] nvarchar(256) NOT NULL,
    [ProtectedReply] nvarchar(max) NULL,
    [ProtectedEvidence] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ReservedTokens] bigint NOT NULL,
    CONSTRAINT [PK_AiTurns] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AiTurns_AiConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiConversations] ([Id]) ON DELETE CASCADE
);
GO


CREATE INDEX [IX_AiConversations_ExpiresAt] ON [AiConversations] ([ExpiresAt]);
GO


CREATE INDEX [IX_AiConversations_OwnerHash_AssistantId] ON [AiConversations] ([OwnerHash], [AssistantId]);
GO


CREATE INDEX [IX_AiMessages_ConversationId_CreatedAt] ON [AiMessages] ([ConversationId], [CreatedAt]);
GO


CREATE INDEX [IX_AiSecurityAudit_CreatedAt] ON [AiSecurityAudit] ([CreatedAt]);
GO


CREATE UNIQUE INDEX [IX_AiTurns_ConversationId_ClientRequestId] ON [AiTurns] ([ConversationId], [ClientRequestId]);
GO


CREATE INDEX [IX_AiUsage_AssistantId_CreatedAt] ON [AiUsage] ([AssistantId], [CreatedAt]);
GO


CREATE INDEX [IX_AiUsage_SubjectHash_CreatedAt] ON [AiUsage] ([SubjectHash], [CreatedAt]);
GO



CREATE TABLE [AiKnowledgeAudit] (
    [Id] nvarchar(450) NOT NULL,
    [ActorHash] nvarchar(max) NOT NULL,
    [Operation] nvarchar(max) NOT NULL,
    [TargetId] nvarchar(max) NOT NULL,
    [Outcome] nvarchar(max) NOT NULL,
    [Timestamp] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AiKnowledgeAudit] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiKnowledgeChunks] (
    [Id] nvarchar(450) NOT NULL,
    [VersionId] nvarchar(450) NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AiKnowledgeChunks] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiKnowledgeDocuments] (
    [Id] nvarchar(450) NOT NULL,
    [PublishedVersionId] nvarchar(max) NULL,
    [LatestVersionId] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AiKnowledgeDocuments] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiKnowledgeVersions] (
    [Id] nvarchar(450) NOT NULL,
    [DocumentId] nvarchar(450) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Collection] nvarchar(max) NOT NULL,
    [Audience] nvarchar(max) NOT NULL,
    [RequiredPermission] nvarchar(max) NULL,
    [StoreIds] nvarchar(max) NOT NULL,
    [OwnerId] nvarchar(max) NULL,
    CONSTRAINT [PK_AiKnowledgeVersions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AiPublicationProducts] (
    [ItemId] int NOT NULL, -- Externally assigned ERP item ID, never a generated publication identity.
    [Published] bit NOT NULL,
    [AssetRevisionId] nvarchar(32) NULL, -- ASSUMED: approved current cutout revision. Migration not applied.
    CONSTRAINT [PK_AiPublicationProducts] PRIMARY KEY ([ItemId])
);
GO


CREATE INDEX [IX_AiKnowledgeChunks_VersionId] ON [AiKnowledgeChunks] ([VersionId]);
GO


CREATE INDEX [IX_AiKnowledgeVersions_DocumentId] ON [AiKnowledgeVersions] ([DocumentId]);
GO
