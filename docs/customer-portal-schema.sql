-- Role: Prepares customer-owned profile/bag/reservation storage; review and apply separately to the Identity database. Not executed by this task.
-- Assumed portal schema. Existing Identity tables and user IDs remain unchanged.
CREATE TABLE [CustomerPortals] (
    [Id] nvarchar(450) NOT NULL,
    [Json] nvarchar(max) NOT NULL,
    [Revision] bigint NOT NULL,
    CONSTRAINT [PK_CustomerPortals] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CustomerPortals_AspNetUsers_Id] FOREIGN KEY ([Id]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [CK_CustomerPortals_Json] CHECK (ISJSON([Json]) = 1)
);
