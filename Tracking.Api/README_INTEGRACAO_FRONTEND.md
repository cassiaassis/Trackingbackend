# 📦 Guia Completo de Integração - API de Rastreamento

## 🎯 Objetivo
Integrar o frontend com a API de rastreamento de pedidos em .NET 8 rodando em **localhost**.

---

## 🔌 Configuração da API

### **Base URL:**
http://localhost:5285/api

### **Autenticação:**
- Tipo: **JWT Bearer Token**
- Método: Obter token via endpoint `/auth/authenticate`
- Header: `Authorization: Bearer {token}`
- Validade: 30 minutos

---

## 📡 Endpoints Disponíveis

### **1️⃣ POST /api/auth/authenticate**
Autentica o usuário e retorna um token JWT.

**Request:**
```json
{
  "identifier": "32676652800"
}
```

- **identifier:** CPF (somente números) ou e-mail do cliente

**Response (200 OK):**
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expires_at": "2026-01-12T15:30:00Z"
}
```

### **2️⃣ POST /api/rastreio**
Consulta informações de rastreamento do pedido.

**Headers:**
```
Authorization: Bearer {token_obtido_no_authenticate}
Content-Type: application/json
```

**Request:**
```json
{
  "identificador": "32676652800"
}
```

- **identificador:** Mesmo CPF ou e-mail usado na autenticação

**Response (200 OK) - Pedido Encontrado:**
```json
{
  "code": 200,
  "message": "OK",
  "info": {
    "id": "8064892",
    "number": "ENX8064892-1",
    "date": "10/01/2026",
    "prediction": "15/01/2026",
    "iderp": "PED-2026-001"
  },
  "shippingevents": [
    {
      "code": "BDE",
      "dscode": "Entregue",
      "message": "Objeto entregue ao destinatário",
      "detalhe": "Objeto entregue ao destinatário",
      "complement": "Entregue para JOANNA",
      "dtshipping": "2026-01-15T14:30:00",
      "internalcode": 90
    },
    {
      "code": "OEC",
      "dscode": "Saiu para entrega",
      "message": "Objeto saiu para entrega ao destinatário",
      "detalhe": "Objeto saiu para entrega ao destinatário",
      "complement": null,
      "dtshipping": "2026-01-15T08:15:00",
      "internalcode": 75
    }
  ]
}
```

**Response (404 Not Found) - CPF Não Encontrado:**
```json
{
  "code": 404,
  "message": "CPF ou e-mail não localizado.",
  "info": null,
  "shippingevents": []
}
```

---

## 🧪 CPFs de Teste (Mocks)

Para testar diferentes cenários sem banco de dados:

| CPF | Cenário | HTTP Status | Descrição |
|-----|---------|-------------|-----------|
| `32676652800` | ✅ **Pedido entregue** | 200 | Retorna histórico completo com múltiplos eventos |
| `12676652800` | ⏳ **Em preparação** | 200 | Pedido sem código de rastreio, apenas 1 evento |
| `22676652801` | ❌ **CPF não encontrado** | **404** | Mensagem: "CPF ou e-mail não localizado." |

---

## 🎨 Estrutura dos Dados

### **ShippingEvent (Evento de Rastreio):**
```ts
interface ShippingEvent {
  code: string;              // Código do evento na transportadora (ex: "BDE", "OEC")
  dscode: string;            // Descrição amigável (ex: "Entregue", "Em trânsito")
  message: string;           // Mensagem detalhada do evento
  detalhe: string;           // Detalhes adicionais
  complement: string | null; // Complemento (ex: local, destinatário)
  dtshipping: string;        // Data/hora no formato ISO 8601
  internalcode: number | null; // Código interno (90=Entregue, 75=Saiu para entrega, etc)
}
```

**OrderInfo (Informações do Pedido):**
```ts
interface OrderInfo {
  id: string;         // ID interno do pedido
  number: string;     // Número do pedido (ex: "ENX8064892-1")
  date: string;       // Data do pedido (dd/MM/yyyy)
  prediction: string; // Previsão de entrega (dd/MM/yyyy)
  iderp: string | null; // ID no sistema ERP
}
```

# Response
```ts
interface TrackingResponse {
  code: number;               // Status code (200 = sucesso)
  message: string;            // Mensagem (ex: "OK", "CPF não localizado")
  info: OrderInfo | null;     // Informações do pedido
  shippingevents: ShippingEvent[]; // Lista de eventos de rastreio
} 
```

---

## 🔐 Fluxo de Autenticação

### **Passo a passo:**

1. **Obter Token:**
```js
const response = await fetch('http://localhost:5285/api/auth/authenticate', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ identifier: '32676652800' })
});
const { access_token } = await response.json();
```


2. **Consultar Rastreio:**
```js
const tracking = await fetch('http://localhost:5285/api/rastreio', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${access_token}`
  },
  body: JSON.stringify({ identificador: '32676652800' })
});
const data = await tracking.json();
```

---

## 🚧 Tratamento de Erros

| Code | Descrição                      | Ação                                             |
|------|--------------------------------|--------------------------------------------------|
| 200  | Sucesso                       | Verificar `message` no JSON para status específico |
| 400  | Bad Request                    | Identificador vazio ou inválido                   |
| 401  | Unauthorized                   | Token inválido/expirado - renovar autenticação   |
| 404  | Not Found                      | CPF ou e-mail não localizado                     |
| 502  | Bad Gateway                    | Serviço TPL indisponível - tentar novamente       |

**Exemplo de Tratamento:**
```js
try {
  const response = await fetch('http://localhost:5285/api/rastreio', { ... });
  
  if (response.status === 401) {
    // Token expirado - renovar
    await authenticate(cpf);
    return consultarRastreio(cpf);
  }
  
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }
  
  const data = await response.json();
  
  // Verificar mensagem específica
  if (data.message === "CPF ou e-mail não localizado.") {
    // Exibir mensagem ao usuário
  }
  
} catch (error) {
  console.error('Erro:', error);
}
```

---

## 📦 Cenários de Resposta

### **1. Pedido Entregue (CPF: 32676652800):**
- **HTTP Status:** `200 OK`
- `code`: 200
- `message`: "OK"
- `shippingevents.length`: Múltiplos eventos
- Último evento: `dscode: "Entregue"`, `internalcode: 90`

### **2. Em Preparação (CPF: 12676652800):**
- **HTTP Status:** `200 OK`
- `code`: 200
- `message`: "OK"
- `shippingevents.length`: 1 evento
- Evento: `dscode: "Em preparação"`, `message: "Seu pedido está sendo preparado"`

### **3. CPF Não Encontrado (CPF: 22676652801):**
- **HTTP Status:** `404 Not Found` ⚠️
- `message`: "CPF ou e-mail não localizado."
- Sem campos `code`, `info` ou `shippingevents` no body

---

## 🔄 Refresh de Token

O token expira em **30 minutos**. Implementar lógica de renovação:
```js
let token = null;
let tokenExpiry = null;

async function getValidToken(cpf) {
  const now = new Date();
  
  // Token ainda válido
  if (token && tokenExpiry && now < tokenExpiry) {
    return token;
  }
  
  // Renovar token
  const response = await fetch('http://localhost:5285/api/auth/authenticate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ identifier: cpf })
  });
  
  const data = await response.json();
  token = data.access_token;
  tokenExpiry = new Date(data.expires_at);
  
  return token;
}
```

---

## 🛠️ Requisitos do Frontend

### **CORS:**
✅ A API já está configurada para aceitar requisições de:
- `http://localhost:3000` (React)
- `http://localhost:4200` (Angular)
- `http://localhost:8080` (Vue)
- `http://localhost:5173` (Vite)

### **Headers Obrigatórios:**
```js
{
  'Content-Type': 'application/json',
  'Authorization': 'Bearer {token}' // Apenas no /rastreio
}
```

---

## ✅ Checklist de Implementação

- [ ] Criar serviço/API client com base URL `http://localhost:5285/api`
- [ ] Implementar método `authenticate(cpfOrEmail)`
- [ ] Implementar método `consultarRastreio(cpfOrEmail)`
- [ ] Armazenar token (localStorage/sessionStorage/context)
- [ ] Implementar renovação automática de token
- [ ] **Tratar erro 404 (CPF não encontrado)**
- [ ] Tratar erro 401 (token expirado)
- [ ] Exibir mensagens baseadas em `response.status` e `data.message`
- [ ] Renderizar eventos ordenados por `dtshipping` (mais recente primeiro)
- [ ] Testar com os 3 CPFs de mock

---

## 📝 Exemplo Completo (React + Axios)

```jsx
import axios from 'axios';

const api = axios.create({
  baseURL: 'http://localhost:5285/api',
});

// Interceptador para adicionar o token em todas as requisições
api.interceptors.request.use(async (config) => {
  const token = localStorage.getItem('token');
  
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  
  return config;
});

export async function authenticate(cpf) {
  const response = await api.post('/auth/authenticate', { identifier: cpf });
  const { access_token } = response.data;
  
  // Armazenar token
  localStorage.setItem('token', access_token);
  
  return access_token;
}

export async function consultarRastreio(cpfOrEmail) {
  const response = await api.post('/rastreio', { identificador: cpfOrEmail });
  
  return response.data;
}
```

### **Response Completa:**
```json
{
  "code": 200,
  "message": "OK",
  "info": {
    "id": "8064892",
    "number": "ENX8064892-1",
    "date": "10/01/2026",
    "prediction": "15/01/2026",
    "iderp": "PED-2026-001"
  },
  "shippingevents": [
    {
      "code": "BDE",
      "dscode": "Entregue",
      "message": "Objeto entregue ao destinatário",
      "detalhe": "Objeto entregue ao destinatário",
      "complement": "Entregue para JOANNA",
      "dtshipping": "2026-01-15T14:30:00",
      "internalcode": 90
    },
    {
      "code": "OEC",
      "dscode": "Saiu para entrega",
      "message": "Objeto saiu para entrega ao destinatário",
      "detalhe": "Objeto saiu para entrega ao destinatário",
      "complement": null,
      "dtshipping": "2026-01-15T08:15:00",
      "internalcode": 75
    }
  ]
}
```

---

## 🧪 Teste Rápido (cURL)

### **1. Autenticar Usuário:**
```bash
curl -X POST http://localhost:5285/api/auth/authenticate \
-H "Content-Type: application/json" \
-d '{ "identifier": "32676652800" }'
```

### **2. Consultar Rastreio:**
```bash
curl -X POST http://localhost:5285/api/rastreio \
-H "Content-Type: application/json" \
-H "Authorization: Bearer {token_obtido_no_authenticate}" \
-d '{ "identificador": "32676652800" }'
```

---

**✨ API pronta para integração! Use os CPFs de mock para desenvolvimento sem dependências do banco de dados.**

**Versão:** 1.1  
**Última atualização:** Janeiro 2026  
**Compatível com:** .NET 8.0  
**Changelog:** Adicionado tratamento HTTP 404 para CPF não encontrado






