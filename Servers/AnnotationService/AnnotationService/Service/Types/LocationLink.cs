﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.Runtime.Serialization; 
using ConnectomeDataModel;

namespace Annotation
{
    
    [DataContract]
    public class LocationLink : DataObject
    {
        long _SourceID;
        long _TargetID;
        string _Username; 

        [DataMember]
        public long SourceID
        {
            get { return _SourceID; }
            set { _SourceID = value; }
        }

        [DataMember]
        public long TargetID
        {
            get { return _TargetID; }
            set { _TargetID = value; }
        }

        /*
        [DataMember]
        public string Username
        {
            get { return _Username; }
            set { _Username = value; }
        }
        */

        public LocationLink(ConnectomeDataModel.LocationLink link)
        {
            _SourceID = link.A;
            _TargetID = link.B;
            _Username = link.Username;
        }
    }
     
}
