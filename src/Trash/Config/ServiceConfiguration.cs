﻿using System.ComponentModel.DataAnnotations;

namespace Trash.Config
{
    public abstract class ServiceConfiguration : IServiceConfiguration
    {
        [Required(ErrorMessage = "Property 'base_url' is required")]
        public string BaseUrl { get; init; } = "";

        [Required(ErrorMessage = "Property 'api_key' is required")]
        public string ApiKey { get; init; } = "";

        public abstract bool IsValid(out string msg);
    }
}
