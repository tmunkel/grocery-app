FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Restore needs the full sources: the SDK only adds the Blazor framework assets (blazor.web.js)
# when it sees Razor components, so a csproj-only restore layer would publish without them.
COPY src/ src/
RUN dotnet restore src/GroceryApp/GroceryApp.csproj
RUN dotnet publish src/GroceryApp/GroceryApp.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "GroceryApp.dll"]
