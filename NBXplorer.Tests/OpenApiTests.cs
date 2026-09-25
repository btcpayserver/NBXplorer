using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Routing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NBXplorer.Tests;

public class OpenApiTests
{
	private static readonly HashSet<string> OpenApiMethods =
	[
		"GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", "TRACE"
	];

	[Fact]
	public void OpenApiRoutesMatchControllers()
	{
		var document = LoadDocument();
		var documentedRoutes = document["paths"]!
			.Children<JProperty>()
			.SelectMany(path => path.Value.Children<JProperty>()
				.Where(operation => OpenApiMethods.Contains(operation.Name.ToUpperInvariant()))
				.Select(operation => $"{operation.Name.ToUpperInvariant()} {path.Name}"))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var controllerRoutes = typeof(Startup).Assembly.GetTypes()
			.Where(type => !type.IsAbstract && typeof(Controller).IsAssignableFrom(type))
			.SelectMany(GetControllerRoutes)
			.Append("GET /health")
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var undocumented = controllerRoutes.Except(documentedRoutes).Order().ToArray();
		var stale = documentedRoutes.Except(controllerRoutes).Order().ToArray();
		Assert.True(undocumented.Length == 0 && stale.Length == 0,
			$"Undocumented routes:\n{string.Join('\n', undocumented)}\n\nStale routes:\n{string.Join('\n', stale)}");
	}

	[Fact]
	public void OpenApiOperationsHaveIdsSecurityAndValidPathParameters()
	{
		var document = LoadDocument();
		Assert.Equal("basicAuth", document["security"]![0]!.Children<JProperty>().Single().Name);

		var operationIds = new HashSet<string>();
		foreach (var path in document["paths"]!.Children<JProperty>())
		{
			var placeholders = Regex.Matches(path.Name, "{([^}:?]+)")
				.Select(match => match.Groups[1].Value)
				.ToHashSet();
			foreach (var operation in path.Value.Children<JProperty>()
				         .Where(operation => OpenApiMethods.Contains(operation.Name.ToUpperInvariant())))
			{
				var operationId = operation.Value.Value<string>("operationId");
				Assert.False(string.IsNullOrEmpty(operationId), $"{operation.Name.ToUpperInvariant()} {path.Name} has no operationId.");
				Assert.True(operationIds.Add(operationId!), $"Duplicate operationId: {operationId}");

				var parameters = operation.Value["parameters"]?.Children<JObject>() ?? [];
				var pathParameters = parameters
					.Select(parameter => ResolveParameter(document, parameter))
					.Where(parameter => parameter.Value<string>("in") == "path")
					.Select(parameter => parameter.Value<string>("name")!)
					.ToHashSet();
				Assert.True(placeholders.SetEquals(pathParameters),
					$"{operation.Name.ToUpperInvariant()} {path.Name} placeholders ({string.Join(", ", placeholders)}) " +
					$"do not match path parameters ({string.Join(", ", pathParameters)}).");
			}
		}

		Assert.Empty(document["paths"]!["/health"]!["get"]!["security"]!);
	}

	private static IEnumerable<string> GetControllerRoutes(Type controller)
	{
		var controllerTemplates = controller.GetCustomAttributes<RouteAttribute>()
			.Select(attribute => attribute.Template)
			.ToArray();
		foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
		{
			var attributes = method.GetCustomAttributes().ToArray();
			var httpProviders = attributes
				.Where(attribute => attribute is IRouteTemplateProvider &&
				                    attribute is IActionHttpMethodProvider httpProvider && httpProvider.HttpMethods.Any())
				.Select(attribute => (Route: (IRouteTemplateProvider)attribute, Http: (IActionHttpMethodProvider)attribute))
				.ToArray();
			var routeTemplates = attributes.OfType<IRouteTemplateProvider>()
				.Where(provider => (provider is not IActionHttpMethodProvider httpProvider || !httpProvider.HttpMethods.Any()) && provider.Template is not null)
				.Select(provider => provider.Template!)
				.ToArray();

			foreach (var provider in httpProviders)
			{
				var actionTemplates = provider.Route.Template is not null
					? [provider.Route.Template]
					: routeTemplates.Length is not 0 ? routeTemplates : [string.Empty];
				foreach (var actionTemplate in actionTemplates)
				foreach (var route in CombineRoutes(controllerTemplates, actionTemplate))
				foreach (var expandedRoute in ExpandOptionalSegments(route))
				foreach (var httpMethod in provider.Http.HttpMethods)
					yield return $"{httpMethod.ToUpperInvariant()} /{expandedRoute}";
			}
		}
	}

	private static IEnumerable<string> CombineRoutes(string[] controllerTemplates, string actionTemplate)
	{
		if (actionTemplate.StartsWith("~/") || actionTemplate.StartsWith('/'))
		{
			yield return actionTemplate.TrimStart('~', '/');
			yield break;
		}

		foreach (var controllerTemplate in controllerTemplates)
			yield return string.Join('/', new[] { controllerTemplate.Trim('/'), actionTemplate.Trim('/') }
				.Where(part => part.Length is not 0));
	}

	private static IEnumerable<string> ExpandOptionalSegments(string route)
	{
		var optionalSegment = Regex.Match(route, @"/?\{[^}/]+\?\}");
		if (!optionalSegment.Success)
		{
			yield return route;
			yield break;
		}

		foreach (var expanded in ExpandOptionalSegments(route.Remove(optionalSegment.Index, optionalSegment.Length)))
			yield return expanded;
		foreach (var expanded in ExpandOptionalSegments(route.Replace("?}", "}")))
			yield return expanded;
	}

	private static JObject ResolveParameter(JObject document, JObject parameter)
	{
		var reference = parameter.Value<string>("$ref");
		return reference is null
			? parameter
			: (JObject)document.SelectToken(reference[2..].Replace('/', '.'))!;
	}

	private static JObject LoadDocument()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			var path = Path.Combine(directory.FullName, "NBXplorer", "wwwroot", "api.json");
			if (File.Exists(path))
				return JObject.Parse(File.ReadAllText(path));
			directory = directory.Parent;
		}
		throw new FileNotFoundException("Could not locate NBXplorer/wwwroot/api.json.");
	}
}
