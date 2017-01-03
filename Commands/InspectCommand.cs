using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TShockAPI;

namespace Inspector.Commands
{
	public class InspectCommand : Command
	{
		public InspectCommand()
			: base(Command, "ins", "inspect", "inspector")
		{

		}

		private static void Field(CommandArgs args)
		{
			if (args == null || args.Parameters == null || args.Parameters.Count < 3)
			{
				args.Player.SendErrorMessage("Insufficient arguments, run /ins help");
				return;
			}

			var command = args.Parameters[0];
			var type = args.Parameters[1];
			var fieldName = args.Parameters[2];

			//Find the type
			var at = InspectorHelpers.SearchForSingleType(args.Player, type);
			if (at == null)
				return;

			//Find the field
			var am = at.GetField(fieldName);
			if (am == null)
			{
				args.Player.SendErrorMessage("Invalid field: " + fieldName);
				return;
			}

			var typeFullName = at.FullName;
			var fieldFullName = am.Name;

			//If a value is specified then set it
			if (args.Parameters.Count > 3 && !am.FieldType.IsArray)  //f Terraria.Main autoSave True
			{
				object data = null;
				if (InspectorHelpers.TryGetDataValue(am.FieldType, args.Parameters[3], out data, args.Player))
				{
					am.SetValue(null, data);
				}
				else
				{
					args.Player.SendErrorMessage($"Unsupported type {am.FieldType.FullName} for field {type}.{fieldName}");
					return;
				}
			}

			//Show the value
			string output;
			var v = am.GetValue(null);

			if (v != null)
			{
				if (v is Array)
				{
					HandleArray(args, v as Array, 3);
					return;
				}

				output = v.ToString();

				if (output == v.GetType().FullName)
				{
					output = "[class] ";
				}
			}
			else
			{
				output = "null";
			}

			args.Player.SendSuccessMessage("Value is: " + output);
		}

		private static void HandleArray(CommandArgs args, Array array, int paginationOffset)
		{
			if (args.Parameters.Count > paginationOffset)
			{
				int index;
				if (Int32.TryParse(args.Parameters[paginationOffset], out index) && index >= 0 && index < array.Length)
				{
					var obj = array.GetValue(index);

					args.Player.SendSuccessMessage("Array contents: " + obj);

					var data = obj.GetType()
						.GetFields()
						.Select(x => x.Name + " " + (x.GetValue(obj) ?? "<null>"))
						.Concat(
							obj.GetType()
							.GetProperties()
							.Where(x => x.GetMethod != null)
							.Select(x => x.Name + " " + (x.GetMethod.Invoke(obj, null) ?? "<null>"))
						);


					InspectorHelpers.RenderPagination(args, paginationOffset + 1, data);
				}
				else
				{
					args.Player.SendErrorMessage("Invalid array index, must be 0-" + array.Length);
				}
			}
			else
			{
				args.Player.SendErrorMessage("Array:");
				args.Player.SendErrorMessage($"\tType: {array.GetType().GetElementType().FullName}");
				args.Player.SendErrorMessage($"\tLength: {array.Length}");
			}
		}

		private static void Property(CommandArgs args)
		{
			if (args == null || args.Parameters == null || args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage("Insufficient arguments, run /ins help");
				return;
			}

			var type = args.Parameters[1];
			var propertyName = args.Parameters[2];

			//Find the type
			var at = InspectorHelpers.SearchForSingleType(args.Player, type);
			if (at == null)
				return;

			//Find the field
			var am = at.GetProperty(propertyName);
			if (am == null)
			{
				args.Player.SendErrorMessage("Invalid property: " + propertyName);
				return;
			}

			var typeFullName = at.FullName;
			var propertyFullName = am.Name;

			//If a value is specified then set it
			if (args.Parameters.Count > 3 && !am.PropertyType.IsArray) //p Terraria.Program LoadedPercentage 0
			{
				object data = null;
				if (InspectorHelpers.TryGetDataValue(am.PropertyType, args.Parameters[3], out data, args.Player))
				{
					if (am.SetMethod != null)
					{
						am.SetMethod.Invoke(null, new[] { data });
					}
					else
					{
						args.Player.SendErrorMessage($"{type}.{propertyName} does not have a setter");
						return;
					}
				}
				else
				{
					args.Player.SendErrorMessage($"Unsupported type {am.PropertyType.FullName} for property {typeFullName}.{propertyFullName}");
				}
			}

			if (am.GetMethod != null)
			{
				//Show the value
				var v = am.GetMethod.Invoke(null, null);

				string output;
				if (v != null)
				{
					if (v is Array)
					{
						HandleArray(args, v as Array, 3);
						return;
					}

					output = v.ToString();
				}
				else
				{
					output = "null";
				}

				args.Player.SendSuccessMessage("Value is: " + output);
			}
			else
			{
				args.Player.SendErrorMessage($"{typeFullName}.{propertyFullName} does not have a getter");
			}
		}

		private static void Method(CommandArgs args)
		{
			if (args == null || args.Parameters == null || args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage("Insufficient arguments, run /ins help");
				return;
			}

			var type = args.Parameters[1];
			var mthd = args.Parameters[2];

			//Find the type
			var at = InspectorHelpers.SearchForSingleType(args.Player, type);
			if (at == null)
				return;

			//Find the field
			var methodInfo = at.GetMethod(mthd);
			if (methodInfo == null)
			{
				args.Player.SendErrorMessage("Invalid method: " + mthd);
				return;
			}

			object[] parameters;
			if (!InspectorHelpers.TryGetMethodParameters(methodInfo, args, out parameters, 3))
				return;

			var res = methodInfo.Invoke(null, parameters);
			if (methodInfo.ReturnType != typeof(void))
			{
				var result = res == null ? "null" : res.ToString();
				args.Player.SendSuccessMessage("Execute result: " + result);
			}
			else
			{
				args.Player.SendSuccessMessage("Executed void method");
			}
		}

		private static void Help(CommandArgs args)
		{
			args.Player.SendInfoMessage("inspect:");
			args.Player.SendInfoMessage("\tsearch|? <namespace.type.member>");
			args.Player.SendInfoMessage("\tfield|f <namespace.type> <field>");
			args.Player.SendInfoMessage("\tfield|f <namespace.type> <field> <value>");
			args.Player.SendInfoMessage("\tprop|p <namespace.type> <property>");
			args.Player.SendInfoMessage("\tprop|p <namespace.type> <property> <value>");
			args.Player.SendInfoMessage("\tmethod|m <namespace.type> <method>");
			args.Player.SendInfoMessage("\tcasesensitive|cs [f]field|");
		}

		private static void Search(CommandArgs args)
		{
			if (args == null || args.Parameters == null || args.Parameters.Count < 2)
			{
				args.Player.SendErrorMessage("Insufficient arguments, run /ins help");
				return;
			}

			var term = args.Parameters[1];

			var member = InspectorHelpers.SearchForSingleMember(args.Player, term);
			if (member == null)
			{
				return;
			}

			var type = member.DeclaringType;

			var fieldInfo = member as FieldInfo;
			var propertyInfo = member as PropertyInfo;
			var methodInfo = member as MethodInfo;

			object v = null;
			string objType;

			if (fieldInfo != null)
			{
				//If a value is specified then set it
				if (args.Parameters.Count > 2 && !fieldInfo.FieldType.IsArray)  //? Terraria.Main.autoSave True
				{
					object data = null;
					if (InspectorHelpers.TryGetDataValue(fieldInfo.FieldType, args.Parameters[2], out data, args.Player))
					{
						fieldInfo.SetValue(null, data);
					}
					else
					{
						args.Player.SendErrorMessage($"Unsupported type {fieldInfo.FieldType.FullName} for field {type}.{fieldInfo.Name}");
						return;
					}
				}

				v = fieldInfo.GetValue(null);
				objType = "Field";
			}
			else if (propertyInfo != null)
			{
				//If a value is specified then set it
				if (args.Parameters.Count > 2 && !propertyInfo.PropertyType.IsArray)  //? Terraria.Main.expertMode True
				{
					if (propertyInfo.SetMethod != null)
					{
						object data = null;
						if (InspectorHelpers.TryGetDataValue(propertyInfo.PropertyType, args.Parameters[2], out data, args.Player))
						{
							propertyInfo.SetValue(null, data);
						}
						else
						{
							args.Player.SendErrorMessage($"Unsupported type {propertyInfo.PropertyType.FullName} for property {type}.{propertyInfo.Name}");
							return;
						}
					}
					else
					{
						args.Player.SendErrorMessage($"{type.FullName}.{propertyInfo.Name} does not have a setter");
						return;
					}
				}

				if (propertyInfo.GetMethod != null)
				{
					v = propertyInfo.GetMethod.Invoke(null, null);
				}
				else
				{
					args.Player.SendErrorMessage($"{type.FullName}.{propertyInfo.Name} does not have a getter");
					return;
				}
				objType = "Property";
			}
			else if (methodInfo != null)
			{
				object[] parameters;
				if (!InspectorHelpers.TryGetMethodParameters(methodInfo, args, out parameters, 2))
					return;

				v = methodInfo.Invoke(null, parameters);
				objType = "Method";
			}
			else
			{
				args.Player.SendErrorMessage($"{type.FullName}.{fieldInfo.Name} has an unsupported MemberInfo `{member.GetType().FullName}`");
				return;
			}

			string output;
			if (v != null)
			{
				if (v is Array)
				{
					HandleArray(args, v as Array, 2);
					return;
				}

				output = v.ToString();

				if (output == v.GetType().FullName)
				{
					output = "[class] ";
				}
			}
			else
			{
				output = "null";
			}

			args.Player.SendSuccessMessage(objType + " value is: " + output);
		}

		private static void CaseSensitive(CommandArgs args)
		{
			var sensitive = !args.Player.IsCaseSensitive();
			args.Player.SetCaseSensitivity(sensitive);

			args.Player.SendSuccessMessage("Case sensitive: " + sensitive);
		}

		private static void Command(CommandArgs args)
		{
			string cmd = args.Parameters.Count == 0 ? "help" : args.Parameters[0].ToLower();

			switch (cmd)
			{
				case "?":
				case "search":
					Search(args);
					break;
				case "f":
				case "field":
					Field(args);
					break;
				case "p":
				case "prop":
					Property(args);
					break;
				case "m":
				case "method":
					Method(args);
					break;
				case "h":
				case "help":
					Help(args);
					break;
				case "cs":
				case "casesensitive":
					CaseSensitive(args);
					break;
				default:
					args.Player.SendErrorMessage("Unsupported inspect command: " + cmd);
					break;
			}
		}
	}
}
