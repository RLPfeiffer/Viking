-- ================================================
-- Template generated from Template Explorer using:
-- Create Inline Function (New Menu).SQL
--
-- Use the Specify Values for Template Parameters 
-- command (Ctrl-Shift-M) to fill in the parameter 
-- values below.
--
-- This block of comments will not be included in
-- the definition of the function.
-- ================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE FUNCTION ufnLinkedToLocations 
(	
	-- Add the parameters for the function here
	 @SourceLocIDs integer_list READONLY --The location IDs we are starting from
)
RETURNS @Links TABLE ( 
	SourceID bigint, --One of the Location IDs from @SourceLocIDs
	TargetID bigint  --The ID of a linked location
	)
AS
BEGIN
	INSERT @Links
		select LL.B, LL.A from LocationLink LL
		inner join @SourceLocIDs B on B.ID = LL.B
		union
		select LL.A, LL.B from LocationLink LL
		inner join @SourceLocIDs A on A.ID = LL.A 
	RETURN
END
GO
