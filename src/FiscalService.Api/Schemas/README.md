# Schemas XSD do DFe.NET

Este diretório deve conter os schemas XSD necessários para validação dos documentos fiscais.

## Como obter

Os schemas são distribuídos junto com o repositório DFe.NET:

```
https://github.com/ZeusAutomacao/DFe.NET/tree/master/NFe.AppTeste/Schemas
```

## Estrutura esperada

```
Schemas/
├── NFe/
│   ├── nfe_v4.00.xsd
│   ├── leiauteNFe_v4.00.xsd
│   ├── leiauteInutNFe_v4.00.xsd
│   ├── leiauteConsReciNFe_v4.00.xsd
│   ├── leiauteConsStatServ_v4.00.xsd
│   ├── leiauteConsSitNFe_v4.00.xsd
│   ├── leiauteNFeProc_v4.00.xsd
│   └── enviNFe_v4.00.xsd
├── NFCe/
│   └── (schemas NFC-e 4.00)
├── CTe/
│   └── (schemas CT-e 4.00)
└── MDFe/
    └── (schemas MDF-e 3.00)
```

## Script de download (Linux/Docker)

```bash
git clone --depth 1 https://github.com/ZeusAutomacao/DFe.NET.git /tmp/DFeNET
cp -r /tmp/DFeNET/NFe.AppTeste/Schemas/* /app/schemas/
```

## Nota sobre Docker

No `Dockerfile`, os schemas são copiados para `/app/schemas/` durante o build. 
Em produção, o diretório é montado como volume e deve ser populado antes do primeiro uso.
