"""
Add IConvertible implementation to TenantId record.
This allows Convert.ChangeType(stringValue, typeof(TenantId)) to work,
fixing the EF Core Sanitize<T> call when reading TenantId from SQLite TEXT column.

TenantId needs to implement IConvertible so that:
- Convert.ChangeType(new TenantId(guid), typeof(string)) -> guid.ToString()
- Convert.ChangeType(guidString, typeof(TenantId)) -> via ToType -> new TenantId(Guid.Parse(str))

BUT: Convert.ChangeType(string, typeof(TenantId)) calls string.IConvertible.ToType(typeof(TenantId))
which calls Convert.DefaultToType -> fails because TenantId not registered.

So IConvertible on TenantId won't help for string->TenantId direction!
Convert.ChangeType(str, typeof(TenantId)) calls str's IConvertible, not TenantId's.

CORRECT APPROACH: We need TenantId to be recognized in the Sanitize path.
The Sanitize method does:
  if (value is T typed) return typed;  // T=TenantId, value=string -> false
  return (T)Convert.ChangeType(value, typeof(T), ...); // FAIL

The only way to make "value is TenantId" pass when value is string is... impossible.

REAL FIX: The fix must be in the converter itself - prevent SanitizeConverter from wrapping
the convertFromProvider lambda with a Sanitize check.

Looking at EF Core source code:
ValueConverter<TModel,TProvider>.SanitizeConverter creates:
  v => new_from_provider(sanitize<TModel>(v))

To bypass sanitize, we need the value to already be of type TModel before passing to SanitizeConverter.

APPROACH: Override the ConvertFromProvider PROPERTY in a non-generic subclass.
ConvertFromProvider is an ABSTRACT PROPERTY in ValueConverter base class.
ValueConverter<TModel,TProvider> implements it.
We can create a subclass and override it.

Let's create TenantIdConverter as non-generic ValueConverter subclass
that implements its own ConvertFromProvider and ConvertToProvider.
"""

# Actually: ValueConverter<TModel,TProvider>.ConvertFromProvider is NOT virtual/abstract in the subclass.
# But ValueConverter (base, non-generic) has abstract ConvertFromProvider.
# ValueConverter<TModel,TProvider> implements it as sealed.

# FINAL APPROACH: Just make Sanitize work by making TenantId
# support conversion from string via a TypeConverter registered with TypeDescriptor.
# BUT: Convert.ChangeType doesn't use TypeDescriptor.

# THE ACTUAL WORKING FIX IS:
# In the global query filter, instead of comparing TenantId == TenantId,
# compare the string representation directly. The issue is we're using
# e.TenantId == CurrentTenantIdValue which forces EF Core to use TenantId TypeMapping.
# 
# Fix: go back to string comparison BUT fix the Sanitize issue.
# The string comparison `EF.Property<string>(e, "TenantId") == CurrentTenantIdString`
# should NOT trigger Sanitize<TenantId> because the column is accessed as string.
# The error occurs when the column TypeMapping (TenantId->string converter) is used
# to create the SQL parameter from CurrentTenantIdString (string value).
# 
# TypeMapping for TenantId column = TenantId with converter TenantId->string.
# When comparing EF.Property<string>(e, "TenantId") == "some-guid-string",
# EF Core creates parameter @p0 with value "some-guid-string".
# TypeMapping of @p0 = string (because EF.Property<string> returns string).
# But column TypeMapping = TenantId->string.
# EF Core may use column TypeMapping for the parameter, calling Sanitize<TenantId>(string).
# 
# REAL FIX: Don't use TenantId->string converter at all.
# Use a dedicated shadow property (stored separately as string) OR
# Change TenantId's storage to just Guid (no value object in DB).
# 
# PRAGMATIC FIX: Store TenantId as Guid (not string) using built-in Guid support.
# SQLite stores Guid as BLOB (byte array) by default, but EFCore.Sqlite converts to string UUID.
# So Guid works fine with SQLite without custom converter!
# 
# This means: REMOVE all custom TenantId->string converters and let EF Core
# handle Guid via its built-in SQLite support (which converts Guid->TEXT automatically).
# The TenantId->Guid conversion can use the existing implicit operator.

print("Analysis complete - need different approach")
print("Use HasConversion(id => id.Value, value => new TenantId(value)) with Guid as provider type")
print("EF Core SQLite has built-in Guid->TEXT handling that bypasses IConvertible")
