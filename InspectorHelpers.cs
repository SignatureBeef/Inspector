using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TShockAPI;

namespace Inspector
{
	public static class InspectorHelpers
	{
		public static Type[] GetLoadableTypes(this Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				return e.Types.Where(t => t != null).ToArray();
			}
		}

		public static bool IsCaseSensitive(this TShockAPI.TSPlayer player)
		{
			var result = player.GetData<bool?>($"{nameof(InspectorHelpers)}.Data.CaseSensitive");

			return result.HasValue && result.Value;
		}

		public static void SetCaseSensitivity(this TShockAPI.TSPlayer player, bool? value)
		{
			player.SetData($"{nameof(InspectorHelpers)}.Data.CaseSensitive", value);
		}

		public static IEnumerable<Type> SearchTypes(string term, bool caseSensitive)
		{
			StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase;
			if (caseSensitive)
			{
				comparisonType = StringComparison.CurrentCulture;
			}

			var results = AppDomain.CurrentDomain
				.GetAssemblies()
				.SelectMany(asm => asm.GetLoadableTypes())
				.Where(t => t.FullName.IndexOf(term, comparisonType) > -1);

			var singleType = results.SingleOrDefault(r => r.FullName.Equals(term, comparisonType));
			if (singleType != null)
			{
				return new[] { singleType };
			}

			return results;
		}

		public static IEnumerable<TMemberInfo> SearchMembers<TMemberInfo>(Type type, string term, bool caseSensitive, IEnumerable<TMemberInfo> members)
			where TMemberInfo : MemberInfo
		{
			StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase;
			if (caseSensitive)
			{
				comparisonType = StringComparison.CurrentCulture;
			}

			var results = members
				.Where(f => f.Name.IndexOf(term, comparisonType) > -1);

			var singleType = results.SingleOrDefault(r => r.Name.Equals(term, comparisonType));
			if (singleType != null)
			{
				return new[] { singleType };
			}
			return results;
		}

		public static IEnumerable<MemberInfo> FindMember(string term, bool caseSensitive, int? maxResults = null)
		{
			StringComparison comparisonType = StringComparison.CurrentCultureIgnoreCase;
			if (caseSensitive)
			{
				comparisonType = StringComparison.CurrentCulture;
			}

			string typeFullName = null;
			var lastPoint = term.LastIndexOf('.');
			if (lastPoint > 0)
			{
				typeFullName = term.Substring(0, lastPoint);
			}

			Func<Type, bool> typePredicate = (p) =>
			{
				return p.FullName != null
					&& (typeFullName == null || p.FullName.StartsWith(typeFullName, comparisonType));
			};
			Func<MemberInfo, bool> memberPredicate = (p) =>
			{
				return p.DeclaringType != null && p.DeclaringType.FullName != null
					&& (typeFullName == null || p.DeclaringType.FullName.StartsWith(typeFullName, comparisonType))
					&& (p.DeclaringType.FullName + "." + p.Name).Length >= term.Length
					&& (p.DeclaringType.FullName + "." + p.Name).IndexOf(term, comparisonType) > -1;
			};

			var results = AppDomain.CurrentDomain
					.GetAssemblies()
					.SelectMany(asm => asm.GetLoadableTypes().Where(typePredicate))
					.SelectMany(t =>
						t.GetFields().Where(memberPredicate)
						.Union(t.GetProperties().Where(memberPredicate))
						.Union(t.GetMethods().Where(memberPredicate))
					);

			if (maxResults.HasValue)
			{
				results = results.Take(maxResults.Value);
			}

			var singleType = results.SingleOrDefault(r => r.Name.Equals(term, comparisonType));
			if (singleType != null)
			{
				return new[] { singleType };
			}
			return results;
		}

		public static bool TryGetDataValue(Type dataType, string val, out object output, TSPlayer player)
		{
			output = null;

			try
			{
				switch (dataType.Name)
				{
					case "Boolean":
						output = Boolean.Parse(val);
						return true;
					case "Int16":
						output = Int16.Parse(val);
						return true;
					case "Int32":
						output = Int32.Parse(val);
						return true;
					case "Int64":
						output = Int64.Parse(val);
						return true;
					case "Byte":
						output = Byte.Parse(val);
						return true;
					case "Double":
						output = Double.Parse(val);
						return true;
					case "Single":
						output = Single.Parse(val);
						return true;
					case "String":
						output = val;
						return true;
				}
			}
			catch //(Exception ex)
			{
				//player.SendErrorMessage($"Failed to parse value for {dataType.Name}: {ex.Message}");
			}

			return false;
		}

		public static Type SearchForSingleType(TSPlayer player, string term, int maxResults = 5)
		{
			var caseSensitive = player.IsCaseSensitive();

			var types = InspectorHelpers.SearchTypes(term, caseSensitive);
			var count = types.Count();
			if (count == 0)
			{
				player.SendErrorMessage("Invalid type: " + term);
			}
			else if (count == 1)
			{
				return types.Single();
			}
			else
			{
				player.SendErrorMessage("Too many types matching: " + term);

				var items = types.Take(maxResults);

				player.SendErrorMessage($"First {items.Count()}");
				foreach (var item in items)
				{
					var name = item.Name;
					if (item.DeclaringType != null)
					{
						name = item.DeclaringType.FullName + "." + name;
					}
					player.SendErrorMessage(name);
				}
			}

			return null;
		}

		public static MemberInfo SearchForSingleMember(TSPlayer player, string term, int maxResults = 5)
		{
			var caseSensitive = player.IsCaseSensitive();

			var types = InspectorHelpers.FindMember(term, caseSensitive, maxResults);
			var count = types.Count();
			if (count == 0)
			{
				player.SendErrorMessage("Invalid member: " + term);
			}
			else if (count == 1)
			{
				return types.Single();
			}
			else
			{
				player.SendErrorMessage("Too many members matching: " + term);

				player.SendErrorMessage($"First {types.Count()}");
				foreach (var item in types)
				{
					var name = item.Name;
					if (item.DeclaringType != null)
					{
						name = item.DeclaringType.FullName + "." + name;
					}
					player.SendErrorMessage(name);
				}
			}

			return null;
		}

		public static void RenderPagination<T>(CommandArgs args, int parameterIndex, IEnumerable<T> data)
		{
			int page = 0, pageSize = 0;
			if (args.Parameters.Count > parameterIndex)
			{
				if (!Int32.TryParse(args.Parameters[parameterIndex], out page))
				{
					page = 0;
				}
			}

			PaginationTools.Settings settings = null;
			if (TSPlayer.Server == args.Player && args.Parameters.Count > parameterIndex + 1)
			{
				if (Int32.TryParse(args.Parameters[parameterIndex + 1], out pageSize))
				{
					settings = new PaginationTools.Settings()
					{
						MaxLinesPerPage = pageSize
					};
				}
			}

			PaginationTools.SendPage(args.Player, page, data.ToList(), settings: settings);
		}

		public static bool TryGetMethodParameters(MethodInfo info, CommandArgs args, out object[] parameters, int parameterStart)
		{
			var prms = info.GetParameters();
			parameters = null;

			if (prms.Length > 0)
			{
				var inputParameters = args.Parameters.Skip(parameterStart);

				if (inputParameters.Count() != prms.Length)
				{
					var parameterNames = String.Join(",", prms
						.Select(x => x.ParameterType.Name + " " + x.Name)
					);

					args.Player.SendErrorMessage($"{info.DeclaringType.FullName}.{info.Name} expects parameters: {parameterNames}");
					return false;
				}

				parameters = new object[prms.Length];

				for (var x = 0; x < inputParameters.Count(); x++)
				{
					var inputParameter = inputParameters.ElementAt(x);

					object data = null;
					if (TryGetDataValue(prms[x].ParameterType, inputParameter, out data, args.Player))
					{
						parameters[x] = data;
					}
					else
					{
						args.Player.SendErrorMessage($"Unsupported {prms[x].ParameterType.FullName} value `{inputParameter}` for parameter {info.DeclaringType.FullName}.{info.Name}.{prms[x].Name}");
						return false;
					}
				}
			}

			return true;
		}
	}
}
