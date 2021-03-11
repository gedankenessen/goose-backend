﻿using Goose.API.Services;
using Goose.Domain.DTOs;
using Goose.Domain.Models.identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Goose.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<ActionResult<IList<UserDTO>>> GetUsersAsync()
        {
            var Users = await _userService.GetUsersAsync();
            return Ok(Users);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserDTO>> GetUserAsync(string id)
        {
            ObjectId objectId = new ObjectId(id);
            var User = await _userService.GetUser(objectId);
            return Ok(User);
        }

        [HttpPost]
        public async Task<ActionResult<User>> CreateUserAsync([FromBody] User User)
        {
            var newUser = await _userService.CreateNewUserAsync(User);
            return Ok(newUser);
        }

        [HttpPut]
        public async Task<ActionResult<User>> UpdateUserAsync([FromBody] User User)
        {
            var UserToUpdate = await _userService.UpdateUserAsync(User);
            return Ok(UserToUpdate);
        }
    }
}

