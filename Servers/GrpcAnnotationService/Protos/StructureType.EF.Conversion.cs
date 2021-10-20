﻿namespace gRPCAnnotationService.Protos
{
    public static class StructureTypeEFExtensions
    {
        public static Viking.DataModel.Annotation.StructureType ToStructureType(this global::Viking.gRPC.AnnotationTypes.V1.Protos.StructureType src)
        {
            var converted = new Viking.DataModel.Annotation.StructureType
            {
                Id = src.Id,
                ParentId = src.HasParentId ? src.ParentId : default,
                Created = src.Created.ToDateTime(),
                LastModified = src.LastModified.ToDateTime(),
                Notes = src.Notes,
                Username = src.Username,
                Tags = src.StructureTags,
                Abstract = src.Abstract,
                Code = src.Code,
                Color = src.Color,
                Name = src.Name,
                StructureTags = src.StructureTags,
                //MarkupType = src.M
            }; 

            /*
            converted.LocationLinkANavigations = src.Links.Where(l => l > src.Id)
                                                                 .Select(x => new Viking.DataModel.Annotation.LocationLink() { A = src.Id, B = x })
                                                                 .ToList();
            converted.LocationLinkBNavigations = src.Links.Where(l => l < src.Id)
                                                                 .Select(x => new Viking.DataModel.Annotation.LocationLink() { A = x, B = src.Id })
                                                                 .ToList();
            */
            return converted;
        }

        public static void Sync(this global::Viking.gRPC.AnnotationTypes.V1.Protos.StructureType src,
          ref Viking.DataModel.Annotation.StructureType converted)
        {
                converted.Id = src.Id;
                converted.ParentId = src.HasParentId ? src.ParentId : default;
                converted.Created = src.Created.ToDateTime();
                converted.LastModified = src.LastModified.ToDateTime();
                converted.Notes = src.Notes;
                converted.Username = src.Username;
                converted.Tags = src.StructureTags;
                converted.Abstract = src.Abstract;
                converted.Code = src.Code;
                converted.Color = src.Color;
                converted.Name = src.Name;
                converted.StructureTags = src.StructureTags;
                //MarkupType = src.M 
        }


        public static global::Viking.gRPC.AnnotationTypes.V1.Protos.StructureType ToProtobufMessage(this Viking.DataModel.Annotation.StructureType src)
        {
            //var compositeLinks = src.LocationLinkANavigations.ToList();
            //compositeLinks.AddRange(src.LocationLinkBNavigations);

            var value = new global::Viking.gRPC.AnnotationTypes.V1.Protos.StructureType
            {  
                Id = src.Id,
                ParentId = src.ParentId.HasValue ? src.ParentId.Value : 0,
                Created = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(src.Created),
                LastModified = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(src.LastModified),
                Notes = src.Notes,
                Username = src.Username,
                Tags = src.Tags,
                Abstract = src.Abstract,
                Code = src.Code,
                Color = src.Color,
                Name = src.Name,
                StructureTags = src.StructureTags,
                //Markuptype = src.MarkupType
            };

            if(false == src.ParentId.HasValue)
                value.ClearParentId();
            
            //value.Links.AddRange(compositeLinks.Select(ll => ll.A == src.Id ? ll.B : ll.A));

            return value;
        }

    }
}
