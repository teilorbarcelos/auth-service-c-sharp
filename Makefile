.PHONY: infra-up infra-down infra-clean dev test coverage setup lint

infra-up:
	docker compose -f docker-compose.infra.yml up -d

infra-down:
	docker compose -f docker-compose.infra.yml down

infra-clean:
	@echo "🧹 Removendo containers e volumes (resetando banco)..."
	docker compose -f docker-compose.infra.yml down -v

dev:
	DOTNET_USE_POLLING_FILE_WATCHER=1 ASPNETCORE_URLS=http://0.0.0.0:8001 dotnet watch --project src/MageBackend.csproj run

test:
	dotnet test tests/MageBackend.Tests.csproj -m:1

coverage:
	@echo "📊 Gerando relatório de cobertura de código..."
	dotnet test tests/MageBackend.Tests.csproj -m:1 /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
	@echo "\n--- Resumo de Cobertura ---"

setup:
	@echo "⚙️ Instalando ferramentas e hooks..."
	dotnet tool restore
	@echo "✅ Setup completo!"

lint:
	@echo "🔍 Verificando comentários // no código-fonte..."
	@! grep -rn '[^:/]//\|^//' src/ --include='*.cs' | grep -v '///' | grep -v '://' || \
		(echo "❌ Encontrados comentários // no código-fonte" && exit 1)
	@echo "✅ Nenhum comentário // encontrado"
	@echo ""
	@echo "🎨 Executando dotnet format..."
	dotnet format src/MageBackend.csproj --verify-no-changes
