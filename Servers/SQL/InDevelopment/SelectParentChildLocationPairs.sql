BEGIN
	declare @ParentChildPairs udtParentChildLocationPairs
	declare @AllowedZRange float
	set @AllowedZRange = 1

	--Start by finding shapes on the same Z level +/-  as the origin location
	insert into @ParentChildPairs (ChildLocationID, ChildStructureID, ParentLocationID, ParentStructureID)
		select L.ID as ChildLocationID, Child.ID as ChildStructureID, PL.ID as ParentLocationID, Child.ParentID as ParentStructureID
			 --MIN( [dbo].[ufnShapeDistance3D](L.VolumeShape, L.Z, PL.VolumeShape, PL.Z) ) as Distance
		 
		from Location L
		inner join Structure Child on Child.ID = L.ParentID 
		inner join Location PL on PL.ParentID = Child.ParentID
		where Child.ParentID is not NULL
			  AND ABS(L.Z - PL.Z) <= @AllowedZRange  

	--select * from #ChildLocParentLocPairingCandidates

	Declare @MissingLocIDs integer_list
	INSERT INTO @MissingLocIDs select L.ID 
			from Location L
			inner join Structure S on S.ID = L.ParentID
			where S.ParentID is not NULL AND 
				  L.ID NOT IN (Select ChildLocationID from @ParentChildPairs) AND
				  EXISTS (SELECT ID from Location PL where PL.ParentID = S.ID) --Ensure parent structure has at least one location

    --select ID from @MissingLocIDs
	select COUNT(ID) as NumRemaining from @MissingLocIDs 
 
	declare @MaxZ float
	set @MaxZ = (Select Max(L.Z) from Location L) 

--	select TOP 10 ID from @MissingLocIDs
	set @AllowedZRange = @AllowedZRange + 1

	delete from @ParentChildPairs

	WHILE (select COUNT(ID) from @MissingLocIDs) > 1 AND @AllowedZRange < @MaxZ
	BEGIN 
		insert into @ParentChildPairs (ChildLocationID, ChildStructureID, ParentLocationID, ParentStructureID)
			SELECT L.ID as ChildLocationID, Child.ID as ChildStructureID, PL.ID as ParentLocationID, Child.ParentID as ParentStructureID
			 --MIN( [dbo].[ufnShapeDistance3D](L.VolumeShape, L.Z, PL.VolumeShape, PL.Z) ) as Distance
			 from @MissingLocIDs Missing
			inner join Location L on L.ID = Missing.ID
			inner join Structure Child on Child.ID = L.ParentID 
			inner join Location PL on PL.ParentID = Child.ParentID
			where Child.ParentID is not NULL 
				AND ABS(L.Z - PL.Z) <= @AllowedZRange  

		delete from @MissingLocIDs WHERE ID in (Select ChildLocationID from @ParentChildPairs)

		--select COUNT(ID) as NumRemaining from @MissingLocIDs 

		set @AllowedZRange = @AllowedZRange + 1
	end

	select * from @ParentChildPairs
	  
end