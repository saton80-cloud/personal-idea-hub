import { http } from '@kit.NetworkKit';
import {
  LoginRequest,
  LoginResponse,
  PairingResponse,
  PendingMutation,
  PullResponse,
  PushResponse,
  SessionState
} from '../models/Contracts';

interface RedeemRequest {
  ticket: string;
  deviceName: string;
  devicePlatform: string;
}

interface PushRequest {
  deviceId: string;
  mutations: PendingMutation[];
}

export class ApiClient {
  private static readonly shared: ApiClient = new ApiClient();

  static getInstance(): ApiClient {
    return ApiClient.shared;
  }

  normalizeServerUrl(value: string): string {
    let result = value.trim();
    while (result.endsWith('/')) {
      result = result.substring(0, result.length - 1);
    }
    if (!result.startsWith('http://') && !result.startsWith('https://')) {
      result = `http://${result}`;
    }
    return result;
  }

  async login(serverUrl: string, userName: string, password: string): Promise<LoginResponse> {
    const body: LoginRequest = {
      userName: userName,
      password: password,
      deviceName: 'HarmonyOS 手机',
      devicePlatform: 'HarmonyOS'
    };
    return await this.request<LoginResponse>(serverUrl, '/api/auth/login', http.RequestMethod.POST, '', body);
  }

  async redeem(serverUrl: string, ticket: string): Promise<PairingResponse> {
    const body: RedeemRequest = {
      ticket: ticket,
      deviceName: 'HarmonyOS 手机',
      devicePlatform: 'HarmonyOS'
    };
    return await this.request<PairingResponse>(serverUrl, '/api/pairing/redeem', http.RequestMethod.POST, '', body);
  }

  async pull(session: SessionState, since: number): Promise<PullResponse> {
    const path = `/api/sync/pull?since=${since}&limit=500`;
    return await this.request<PullResponse>(session.serverUrl, path, http.RequestMethod.GET, session.accessToken, undefined);
  }

  async push(session: SessionState, mutations: PendingMutation[]): Promise<PushResponse> {
    const body: PushRequest = {
      deviceId: session.deviceId,
      mutations: mutations
    };
    return await this.request<PushResponse>(session.serverUrl, '/api/sync/push', http.RequestMethod.POST,
      session.accessToken, body);
  }

  private async request<T>(serverUrl: string, path: string, method: http.RequestMethod,
    token: string, body: Object | undefined): Promise<T> {
    const client = http.createHttp();
    const headers: Record<string, string> = {
      'Accept': 'application/json',
      'Content-Type': 'application/json'
    };
    if (token.length > 0) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    const options: http.HttpRequestOptions = {
      method: method,
      header: headers,
      expectDataType: http.HttpDataType.STRING,
      connectTimeout: 10000,
      readTimeout: 20000
    };
    if (body !== undefined) {
      options.extraData = JSON.stringify(body);
    }

    try {
      const response = await client.request(`${this.normalizeServerUrl(serverUrl)}${path}`, options);
      const text = response.result as string;
      if (response.responseCode < 200 || response.responseCode >= 300) {
        throw new Error(`服务器返回 ${response.responseCode}：${text}`);
      }
      return JSON.parse(text) as T;
    } finally {
      client.destroy();
    }
  }
}
