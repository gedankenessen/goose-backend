﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Goose.API.Repositories;
using Goose.Domain.DTOs;
using Goose.Domain.DTOs.issues;
using Goose.Domain.Models.tickets;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Goose.API.Services.issues
{
    public interface IIssueService
    {
        Task<IList<IssueDTO>> GetAll();
        public Task<IssueDTO> Get(ObjectId id);
        Task<IList<IssueDTO>> GetAllOfProject(ObjectId projectId);
        public Task<IssueDTO> GetOfProject(ObjectId projectId, ObjectId id);
        public Task<IssueDTO> Create(IssueDTO issueDto);
        public Task<IssueDTO> Create(IssueCreateDTO issueDto);
        public Task<IssueDTO> Update(IssueDTO issueDto, ObjectId id);
        public Task<bool> Delete(ObjectId id);
        public Task<IssueDTO?> GetParent(ObjectId issueId);
        public Task SetParent(ObjectId issueId, ObjectId parentId);
        public Task RemoveParent(ObjectId issueId);
    }

    public class IssueService : IIssueService
    {
        private readonly IStateService _stateService;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;

        private readonly IIssueRepository _issueRepo;

        public IssueService(IIssueRepository issueRepo, IStateService stateService,
            IProjectRepository projectRepository, IUserRepository userRepository)
        {
            _issueRepo = issueRepo;
            _stateService = stateService;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
        }

        public async Task<IList<IssueDTO>> GetAll()
        {
            return (await Task.WhenAll((await _issueRepo.GetAsync()).Select(CreateDtoFromIssue))).ToList();
        }

        public async Task<IssueDTO> Get(ObjectId id)
        {
            return await CreateDtoFromIssue(await _issueRepo.GetAsync(id));
        }

        public async Task<IList<IssueDTO>> GetAllOfProject(ObjectId projectId)
        {
            return (await Task.WhenAll((await _issueRepo.GetAllOfProjectAsync(projectId)).Select(CreateDtoFromIssue))).ToList();
        }

        public async Task<IssueDTO> GetOfProject(ObjectId projectId, ObjectId id)
        {
            return await CreateDtoFromIssue(await _issueRepo.GetOfProjectAsync(projectId, id));
        }

        public async Task<IssueDTO> Create(IssueDTO issueDto)
        {
            var issue = issueDto.ToIssue();
            await _issueRepo.CreateAsync(issue);

            issueDto.Id = issue.Id;
            return issueDto;
        }

        public async Task<IssueDTO> Create(IssueCreateDTO issueDto)
        {
            var issue = issueDto.ToIssue();
            await _issueRepo.CreateAsync(issue);

            return await Get(issue.Id);
        }

        public async Task<IssueDTO> Update(IssueDTO issueDto, ObjectId id)
        {
            var issue = issueDto.IntoIssue(await _issueRepo.GetAsync(id));
            await _issueRepo.UpdateAsync(issue);
            return issueDto;
        }

        public async Task<bool> Delete(ObjectId id)
        {
            return (await _issueRepo.DeleteAsync(id)).DeletedCount > 0;
        }

        public async Task<IssueDTO?> GetParent(ObjectId issueId)
        {
            var parentId = (await _issueRepo.GetAsync(issueId)).ParentIssueId;
            if (parentId == null) return null;
            return await Get(issueId);
        }

        public async Task SetParent(ObjectId issueId, ObjectId parentId)
        {
            var issue = await _issueRepo.GetAsync(issueId);
            issue.ParentIssueId = parentId;
            await _issueRepo.UpdateAsync(issue);
        }

        public async Task RemoveParent(ObjectId issueId)
        {
            var issue = await _issueRepo.GetAsync(issueId);
            issue.ParentIssueId = null;
            await _issueRepo.UpdateAsync(issue);
        }


        private async Task<IssueDTO> CreateDtoFromIssue(Issue issue)
        {
            var state = _stateService.GetState(issue.ProjectId, issue.StateId);
            var project = _projectRepository.GetAsync(issue.ProjectId);
            var client = _userRepository.GetAsync(issue.ClientId);
            var author = _userRepository.GetAsync(issue.AuthorId);
            return new IssueDTO(issue, await state, new ProjectDTO(await project), new UserDTO(await client),
                new UserDTO(await author));
        }
    }
}