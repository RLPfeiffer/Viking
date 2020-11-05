﻿using Annotation.Interfaces;
using AnnotationService.Types;

namespace AnnotationVizLib.WCFClient
{
    class WCFStructureLinkAdapter : IStructureLink
    {
        private StructureLink structureLink;

        public WCFStructureLinkAdapter(StructureLink sl)
        {
            this.structureLink = sl;
        }

        public bool Directional
        {
            get
            {
                return !structureLink.Bidirectional;
            }
        }

        public ulong SourceID
        {
            get
            {
                return (ulong)structureLink.SourceID;
            }
        }

        public ulong TargetID
        {
            get
            {
                return (ulong)structureLink.TargetID;
            }
        }

        public bool Equals(IStructureLink other)
        {
            if (object.ReferenceEquals(other, null))
                return false;

            if (other.SourceID == this.SourceID &&
                other.TargetID == this.TargetID &&
                other.Directional == this.Directional)
                return true;

            return false;
        }
    }
}
