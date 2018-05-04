﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Models.UserViewModels
{
    public class OrganizationDetailsViewModel
    {
        [Required]
        [Display(Name = "ID")]
        public long Id { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Organization")]
        public string Name { get; set; }

        /// <summary>
        /// All users in the database, the selected property is true if they are a member of the organization
        /// </summary>
        [Display(Name = "Users")]
        public List<UserSelectedViewModel> UserList { get; set; }
    }
}
