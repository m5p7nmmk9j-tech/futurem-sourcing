import axios from 'axios'

export const http = axios.create({
  baseURL: '/api',
  timeout: 15000
})

http.interceptors.response.use(
  response => response,
  error => {
    console.error('API Error:', error)
    return Promise.reject(error)
  }
)
