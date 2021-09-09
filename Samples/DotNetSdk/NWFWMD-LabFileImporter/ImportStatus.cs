﻿using System;
using System.Net;

namespace NWFWMDLabFileImporter
{
    public class ImportStatus
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public Uri ResultUri { get; set; }

        //If API is changed to not return redirect, we are still OK if result Uri is returned:
        public bool IsImportFinished => HttpStatusCode == HttpStatusCode.Redirect || ResultUri != null;
    }
}
