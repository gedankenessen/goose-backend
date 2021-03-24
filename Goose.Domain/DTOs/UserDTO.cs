﻿using Goose.Data.Models;
using Goose.Domain.Models.identity;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Goose.Domain.Models.identity;

namespace Goose.Domain.DTOs
{
    public class UserDTO
    {
        public UserDTO()
        {
        }

        public ObjectId Id { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }

        public UserDTO()
        {

        }

        public UserDTO(User user)
        {
            Id = user.Id;
            Firstname = user.Firstname;
            Lastname = user.Lastname;
        }
    }
}